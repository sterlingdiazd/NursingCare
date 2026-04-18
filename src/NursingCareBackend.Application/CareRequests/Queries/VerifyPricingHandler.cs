using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Queries;

public sealed record PricingFieldComparison(
    string FieldName,
    decimal Stored,
    decimal Current,
    decimal Difference,
    bool Matches);

public sealed record PricingVerificationResult(
    Guid CareRequestId,
    bool Matches,
    decimal ToleranceUsed,
    IReadOnlyList<string> LimitationNotes,
    IReadOnlyList<PricingFieldComparison> FieldComparisons);

public sealed class VerifyPricingHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly ICareRequestPricingCalculator _pricingCalculator;

    private const decimal Tolerance = 0.01m;

    public VerifyPricingHandler(
        ICareRequestRepository repository,
        ICareRequestPricingCalculator pricingCalculator)
    {
        _repository = repository;
        _pricingCalculator = pricingCalculator;
    }

    /// <summary>
    /// Returns null if the care request is not found.
    /// Returns a result with a 422-signalling flag when snapshot fields are all null.
    /// </summary>
    public async Task<PricingVerificationResult?> HandleAsync(
        Guid careRequestId,
        CancellationToken cancellationToken)
    {
        var careRequest = await _repository.GetByIdAsync(
            careRequestId,
            CareRequestAccessScope.Admin,
            cancellationToken);

        if (careRequest is null)
        {
            return null;
        }

        if (!careRequest.LineBeforeVolumeDiscount.HasValue
            || !careRequest.UnitPriceAfterVolumeDiscount.HasValue
            || !careRequest.SubtotalBeforeSupplies.HasValue)
        {
            return new PricingVerificationResult(
                CareRequestId: careRequestId,
                Matches: false,
                ToleranceUsed: Tolerance,
                LimitationNotes: new[]
                {
                    "Este registro fue creado antes de que se implementaran los snapshots de precios intermedios. La verificacion no esta disponible."
                },
                FieldComparisons: Array.Empty<PricingFieldComparison>());
        }

        var existingSameUnitTypeCount = await _repository.CountByUserAndUnitTypeAsync(
            careRequest.UserID,
            careRequest.UnitType,
            cancellationToken);

        var command = new CreateCareRequestCommand
        {
            UserID = careRequest.UserID,
            Description = careRequest.Description,
            CareRequestReason = careRequest.CareRequestReason,
            CareRequestType = careRequest.CareRequestType,
            SuggestedNurse = careRequest.SuggestedNurse,
            AssignedNurse = careRequest.AssignedNurse,
            Unit = careRequest.Unit,
            Price = careRequest.Price,
            ClientBasePriceOverride = careRequest.ClientBasePrice,
            DistanceFactor = careRequest.DistanceFactor,
            ComplexityLevel = careRequest.ComplexityLevel,
            MedicalSuppliesCost = careRequest.MedicalSuppliesCost,
            CareRequestDate = careRequest.CareRequestDate,
        };

        var current = await _pricingCalculator.CalculateAsync(command, existingSameUnitTypeCount, cancellationToken);

        var comparisons = new List<PricingFieldComparison>
        {
            Compare("Price", careRequest.Price, current.Price),
            Compare("Total", careRequest.Total, current.Total),
            Compare("CategoryFactorSnapshot", careRequest.CategoryFactorSnapshot ?? 0m, current.CategoryFactorSnapshot),
            Compare("DistanceFactorMultiplierSnapshot", careRequest.DistanceFactorMultiplierSnapshot ?? 0m, current.DistanceFactorMultiplierSnapshot),
            Compare("ComplexityMultiplierSnapshot", careRequest.ComplexityMultiplierSnapshot ?? 0m, current.ComplexityMultiplierSnapshot),
            Compare("LineBeforeVolumeDiscount", careRequest.LineBeforeVolumeDiscount!.Value, current.LineBeforeVolumeDiscount),
            Compare("UnitPriceAfterVolumeDiscount", careRequest.UnitPriceAfterVolumeDiscount!.Value, current.UnitPriceAfterVolumeDiscount),
            Compare("SubtotalBeforeSupplies", careRequest.SubtotalBeforeSupplies!.Value, current.SubtotalBeforeSupplies),
        };

        var allMatch = comparisons.All(comparison => comparison.Matches);

        var limitationNotes = new List<string>
        {
            $"La verificacion del descuento por volumen usa el conteo actual de solicitudes del mismo tipo de unidad ({existingSameUnitTypeCount}), " +
            "que puede diferir del conteo al momento de la creacion original. El porcentaje de descuento por volumen no forma parte de la comparacion de campos."
        };

        return new PricingVerificationResult(
            CareRequestId: careRequestId,
            Matches: allMatch,
            ToleranceUsed: Tolerance,
            LimitationNotes: limitationNotes,
            FieldComparisons: comparisons.AsReadOnly());
    }

    private static PricingFieldComparison Compare(string fieldName, decimal stored, decimal current)
    {
        var difference = Math.Abs(stored - current);
        return new PricingFieldComparison(
            FieldName: fieldName,
            Stored: stored,
            Current: current,
            Difference: difference,
            Matches: difference <= Tolerance);
    }
}

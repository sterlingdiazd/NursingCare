using NursingCareBackend.Application.CareRequests.Queries;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed record PricingValuesSnapshot(
    decimal Price,
    decimal Total,
    decimal CategoryFactorSnapshot,
    decimal DistanceFactorMultiplierSnapshot,
    decimal ComplexityMultiplierSnapshot,
    decimal LineBeforeVolumeDiscount,
    decimal UnitPriceAfterVolumeDiscount,
    decimal SubtotalBeforeSupplies);

public sealed record PricingDiscrepancy(
    string FieldName,
    decimal StoredValue,
    decimal CurrentValue,
    decimal Difference);

public sealed record PricingVerificationResponse(
    Guid CareRequestId,
    bool Matches,
    decimal ToleranceUsed,
    IReadOnlyList<string> LimitationNotes,
    IReadOnlyList<PricingDiscrepancy> Discrepancies)
{
    public static PricingVerificationResponse FromResult(PricingVerificationResult result)
    {
        var discrepancies = result.FieldComparisons
            .Where(comparison => !comparison.Matches)
            .Select(comparison => new PricingDiscrepancy(
                comparison.FieldName,
                comparison.Stored,
                comparison.Current,
                comparison.Difference))
            .ToList()
            .AsReadOnly();

        return new PricingVerificationResponse(
            CareRequestId: result.CareRequestId,
            Matches: result.Matches,
            ToleranceUsed: result.ToleranceUsed,
            LimitationNotes: result.LimitationNotes,
            Discrepancies: discrepancies);
    }
}

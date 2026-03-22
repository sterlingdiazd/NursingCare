using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Catalogs;

public sealed class CareRequestPricingCalculator : ICareRequestPricingCalculator, IPricingPreviewService
{
    private readonly NursingCareDbContext _dbContext;

    public CareRequestPricingCalculator(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> ResolveUnitTypeAsync(string careRequestTypeCode, CancellationToken cancellationToken)
    {
        var typeRow = await _dbContext.CareRequestTypeCatalogs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Code == careRequestTypeCode && row.IsActive,
                cancellationToken);

        if (typeRow is null)
        {
            throw new ArgumentException($"Unknown or inactive care_request_type '{careRequestTypeCode}'.", nameof(careRequestTypeCode));
        }

        return typeRow.UnitTypeCode;
    }

    public Task<CareRequestPricingResult> CalculateAsync(
        CreateCareRequestCommand command,
        int existingSameUnitTypeCount,
        CancellationToken cancellationToken)
        => CalculateInternalAsync(
            careRequestTypeCode: command.CareRequestType,
            unit: command.Unit,
            priceOverride: command.Price,
            clientBasePriceOverride: command.ClientBasePriceOverride,
            distanceFactorCode: command.DistanceFactor,
            complexityLevelCode: command.ComplexityLevel,
            medicalSuppliesCost: command.MedicalSuppliesCost,
            existingSameUnitTypeCount: existingSameUnitTypeCount,
            useProposedOverrides: false,
            proposed: null,
            cancellationToken);

    public async Task<PricingPreviewResponse> PreviewAsync(
        PricingPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var r = await CalculateInternalAsync(
            careRequestTypeCode: request.CareRequestTypeCode,
            unit: request.Unit,
            priceOverride: request.PriceOverride,
            clientBasePriceOverride: request.ClientBasePriceOverride,
            distanceFactorCode: request.DistanceFactorCode,
            complexityLevelCode: request.ComplexityLevelCode,
            medicalSuppliesCost: request.MedicalSuppliesCost,
            existingSameUnitTypeCount: request.ExistingSameUnitTypeCount,
            useProposedOverrides: request.UseProposedOverrides,
            proposed: request.ProposedOverrides,
            cancellationToken);

        return new PricingPreviewResponse(
            UnitType: r.UnitType,
            PricingCategoryCode: r.PricingCategoryCode,
            BasePrice: r.Price,
            CategoryFactor: r.CategoryFactorSnapshot,
            DistanceMultiplier: r.DistanceFactorMultiplierSnapshot,
            ComplexityMultiplier: r.ComplexityMultiplierSnapshot,
            VolumeDiscountPercent: r.VolumeDiscountPercentSnapshot,
            LineBeforeVolumeDiscount: r.LineBeforeVolumeDiscount,
            UnitPriceAfterVolumeDiscount: r.UnitPriceAfterVolumeDiscount,
            SubtotalBeforeSupplies: r.SubtotalBeforeSupplies,
            MedicalSuppliesCost: r.MedicalSuppliesCost,
            GrandTotal: r.Total);
    }

    private async Task<CareRequestPricingResult> CalculateInternalAsync(
        string careRequestTypeCode,
        int unit,
        decimal? priceOverride,
        decimal? clientBasePriceOverride,
        string? distanceFactorCode,
        string? complexityLevelCode,
        decimal? medicalSuppliesCost,
        int existingSameUnitTypeCount,
        bool useProposedOverrides,
        PricingPreviewOverrides? proposed,
        CancellationToken cancellationToken)
    {
        if (unit <= 0)
        {
            throw new ArgumentException("Unit must be greater than zero.", nameof(unit));
        }

        var typeRow = await _dbContext.CareRequestTypeCatalogs
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Code == careRequestTypeCode, cancellationToken);

        if (typeRow is null || !typeRow.IsActive)
        {
            throw new ArgumentException($"Unknown or inactive care_request_type '{careRequestTypeCode}'.", nameof(careRequestTypeCode));
        }

        var categoryRow = await _dbContext.CareRequestCategoryCatalogs
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Code == typeRow.CareRequestCategoryCode && row.IsActive, cancellationToken);

        if (categoryRow is null)
        {
            throw new InvalidOperationException(
                $"Care request category '{typeRow.CareRequestCategoryCode}' is missing or inactive.");
        }

        var categoryCode = categoryRow.Code;
        var categoryFactor = useProposedOverrides && proposed?.CategoryFactor is { } cf
            ? cf
            : categoryRow.CategoryFactor;

        if (categoryFactor <= 0)
        {
            categoryFactor = 1.0m;
        }

        var basePriceFromCatalog = useProposedOverrides && proposed?.BasePrice is { } pb
            ? pb
            : typeRow.BasePrice;

        var basePrice = priceOverride is { } p && p > 0
            ? p
            : clientBasePriceOverride is { } cbp && cbp > 0
                ? cbp
                : basePriceFromCatalog;

        if (basePrice <= 0)
        {
            basePrice = 60m;
        }

        var isDomicilio = string.Equals(categoryCode, "domicilio", StringComparison.OrdinalIgnoreCase);
        var isHogarOrDomicilio = string.Equals(categoryCode, "hogar", StringComparison.OrdinalIgnoreCase)
            || isDomicilio;
        string? resolvedDistanceCode = null;
        decimal distanceMultiplier = 1.0m;
        if (isDomicilio)
        {
            resolvedDistanceCode = string.IsNullOrWhiteSpace(distanceFactorCode) ? "local" : distanceFactorCode.Trim();
            if (useProposedOverrides && proposed?.DistanceMultiplier is { } dm)
            {
                distanceMultiplier = dm;
            }
            else
            {
                var distanceRow = await _dbContext.DistanceFactorCatalogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        row => row.Code == resolvedDistanceCode && row.IsActive,
                        cancellationToken);

                if (distanceRow is null)
                {
                    throw new ArgumentException("Distance factor is not valid.", nameof(distanceFactorCode));
                }

                distanceMultiplier = distanceRow.Multiplier;
            }
        }

        string? resolvedComplexityCode = null;
        decimal complexityMultiplier = 1.0m;
        if (isHogarOrDomicilio)
        {
            resolvedComplexityCode = string.IsNullOrWhiteSpace(complexityLevelCode) ? "estandar" : complexityLevelCode.Trim();
            if (useProposedOverrides && proposed?.ComplexityMultiplier is { } cm)
            {
                complexityMultiplier = cm;
            }
            else
            {
                var complexityRow = await _dbContext.ComplexityLevelCatalogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        row => row.Code == resolvedComplexityCode && row.IsActive,
                        cancellationToken);

                if (complexityRow is null)
                {
                    throw new ArgumentException("Complexity level is not valid.", nameof(complexityLevelCode));
                }

                complexityMultiplier = complexityRow.Multiplier;
            }
        }

        var volumeDiscountPercent = useProposedOverrides && proposed?.VolumeDiscountPercent is { } vd
            ? vd
            : await ResolveVolumeDiscountPercentAsync(existingSameUnitTypeCount, cancellationToken);

        var lineBeforeVolumeDiscount = basePrice
            * categoryFactor
            * distanceMultiplier
            * complexityMultiplier;

        var unitPriceAfterVolumeDiscount = lineBeforeVolumeDiscount * (1 - volumeDiscountPercent / 100m);

        var supplies = medicalSuppliesCost ?? 0m;
        if (supplies < 0)
        {
            throw new ArgumentException("MedicalSuppliesCost must be >= 0 when provided.", nameof(medicalSuppliesCost));
        }

        var subtotalBeforeSupplies = unitPriceAfterVolumeDiscount * unit;
        var grandTotal = subtotalBeforeSupplies + supplies;

        if (grandTotal < 0)
        {
            throw new InvalidOperationException("Calculated total cannot be negative.");
        }

        return new CareRequestPricingResult(
            UnitType: typeRow.UnitTypeCode,
            PricingCategoryCode: categoryCode,
            Price: decimal.Round(basePrice, 2, MidpointRounding.AwayFromZero),
            Total: decimal.Round(grandTotal, 2, MidpointRounding.AwayFromZero),
            CategoryFactorSnapshot: categoryFactor,
            DistanceFactorMultiplierSnapshot: distanceMultiplier,
            ComplexityMultiplierSnapshot: complexityMultiplier,
            VolumeDiscountPercentSnapshot: volumeDiscountPercent,
            DistanceFactorCode: isDomicilio ? resolvedDistanceCode : null,
            ComplexityLevelCode: isHogarOrDomicilio ? resolvedComplexityCode : null,
            LineBeforeVolumeDiscount: decimal.Round(lineBeforeVolumeDiscount, 4, MidpointRounding.AwayFromZero),
            UnitPriceAfterVolumeDiscount: decimal.Round(unitPriceAfterVolumeDiscount, 4, MidpointRounding.AwayFromZero),
            SubtotalBeforeSupplies: decimal.Round(subtotalBeforeSupplies, 2, MidpointRounding.AwayFromZero),
            MedicalSuppliesCost: decimal.Round(supplies, 2, MidpointRounding.AwayFromZero));
    }

    private async Task<int> ResolveVolumeDiscountPercentAsync(int existingSameUnitTypeCount, CancellationToken cancellationToken)
    {
        if (existingSameUnitTypeCount <= 0)
        {
            return 0;
        }

        var rules = await _dbContext.VolumeDiscountRules
            .AsNoTracking()
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.MinimumCount)
            .ToListAsync(cancellationToken);

        var applicable = 0;
        foreach (var rule in rules)
        {
            if (existingSameUnitTypeCount >= rule.MinimumCount)
            {
                applicable = rule.DiscountPercent;
            }
        }

        return applicable;
    }
}

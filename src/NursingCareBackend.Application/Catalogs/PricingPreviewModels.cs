namespace NursingCareBackend.Application.Catalogs;

public sealed class PricingPreviewRequest
{
    public string CareRequestTypeCode { get; init; } = default!;

    public int Unit { get; init; } = 1;

    public decimal? PriceOverride { get; init; }

    public decimal? ClientBasePriceOverride { get; init; }

    public string? DistanceFactorCode { get; init; }

    public string? ComplexityLevelCode { get; init; }

    public decimal? MedicalSuppliesCost { get; init; }

    public int ExistingSameUnitTypeCount { get; init; }

    /// <summary>
    /// When true, multipliers are taken from <see cref="ProposedOverrides"/> when provided instead of the database.
    /// </summary>
    public bool UseProposedOverrides { get; init; }

    public PricingPreviewOverrides? ProposedOverrides { get; init; }
}

public sealed class PricingPreviewOverrides
{
    public decimal? CategoryFactor { get; init; }

    public decimal? DistanceMultiplier { get; init; }

    public decimal? ComplexityMultiplier { get; init; }

    public int? VolumeDiscountPercent { get; init; }

    public decimal? BasePrice { get; init; }
}

public sealed record PricingPreviewResponse(
    string UnitType,
    string PricingCategoryCode,
    decimal BasePrice,
    decimal CategoryFactor,
    decimal DistanceMultiplier,
    decimal ComplexityMultiplier,
    int VolumeDiscountPercent,
    decimal LineBeforeVolumeDiscount,
    decimal UnitPriceAfterVolumeDiscount,
    decimal SubtotalBeforeSupplies,
    decimal MedicalSuppliesCost,
    decimal GrandTotal);

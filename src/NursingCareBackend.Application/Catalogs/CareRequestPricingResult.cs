namespace NursingCareBackend.Application.Catalogs;

public sealed record CareRequestPricingResult(
    string UnitType,
    string PricingCategoryCode,
    decimal Price,
    decimal Total,
    decimal CategoryFactorSnapshot,
    decimal DistanceFactorMultiplierSnapshot,
    decimal ComplexityMultiplierSnapshot,
    int VolumeDiscountPercentSnapshot,
    string? DistanceFactorCode,
    string? ComplexityLevelCode,
    decimal LineBeforeVolumeDiscount,
    decimal UnitPriceAfterVolumeDiscount,
    decimal SubtotalBeforeSupplies,
    decimal MedicalSuppliesCost);

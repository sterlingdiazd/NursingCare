namespace NursingCareBackend.Domain.CareRequests;

public sealed record CareRequestCreateParams
{
    public required Guid UserID { get; init; }
    public required string Description { get; init; }
    public string? CareRequestReason { get; init; }
    public required string CareRequestType { get; init; }
    public required string UnitType { get; init; }
    public string? SuggestedNurse { get; init; }
    public Guid? AssignedNurse { get; init; }
    public required int Unit { get; init; }
    public required decimal Price { get; init; }
    public required decimal Total { get; init; }
    public decimal? ClientBasePrice { get; init; }
    public string? DistanceFactor { get; init; }
    public string? ComplexityLevel { get; init; }
    public decimal? MedicalSuppliesCost { get; init; }
    public DateOnly? CareRequestDate { get; init; }
    public required string PricingCategoryCode { get; init; }
    public required decimal CategoryFactorSnapshot { get; init; }
    public required decimal DistanceFactorMultiplierSnapshot { get; init; }
    public required decimal ComplexityMultiplierSnapshot { get; init; }
    public required int VolumeDiscountPercentSnapshot { get; init; }
    public decimal? LineBeforeVolumeDiscount { get; init; }
    public decimal? UnitPriceAfterVolumeDiscount { get; init; }
    public decimal? SubtotalBeforeSupplies { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

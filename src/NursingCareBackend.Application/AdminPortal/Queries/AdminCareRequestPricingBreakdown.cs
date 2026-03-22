namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminCareRequestPricingBreakdown(
  string Category,
  decimal BasePrice,
  decimal CategoryFactor,
  string? DistanceFactor,
  decimal DistanceFactorValue,
  string? ComplexityLevel,
  decimal ComplexityFactorValue,
  decimal VolumeDiscountPercent,
  decimal SubtotalBeforeSupplies,
  decimal MedicalSuppliesCost,
  decimal Total);

namespace NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;

public class CreateCareRequestCommand
{
  public Guid UserID { get; init; }
  public string Description { get; init; } = default!;

  public string? CareRequestReason { get; init; }
  public string CareRequestType { get; init; } = default!;
  public Guid? NurseId { get; init; }
  public string? SuggestedNurse { get; init; }
  public string? AssignedNurse { get; init; }

  // Quantity / units
  public int Unit { get; init; } = 1;

  // Base price used in calculations (price always modifiable).
  public decimal? Price { get; init; }

  // Optional base price override; if set and > 0, used when Price is missing/invalid.
  public decimal? ClientBasePriceOverride { get; init; }

  public string? DistanceFactor { get; init; }
  public string? ComplexityLevel { get; init; }

  public decimal? MedicalSuppliesCost { get; init; }
  public DateOnly? CareRequestDate { get; init; }
}

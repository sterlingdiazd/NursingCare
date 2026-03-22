using System.ComponentModel.DataAnnotations;

namespace NursingCareBackend.Api.Controllers.Admin;

public sealed class CreateAdminCareRequestRequest
{
  [Required]
  public Guid ClientUserId { get; set; }

  [Required]
  public string CareRequestDescription { get; set; } = default!;

  [Required]
  public string CareRequestType { get; set; } = default!;

  public int Unit { get; set; } = 1;
  public string? SuggestedNurse { get; set; }
  public decimal? Price { get; set; }
  public decimal? ClientBasePriceOverride { get; set; }
  public string? DistanceFactor { get; set; }
  public string? ComplexityLevel { get; set; }
  public decimal? MedicalSuppliesCost { get; set; }
  public DateOnly? CareRequestDate { get; set; }
}

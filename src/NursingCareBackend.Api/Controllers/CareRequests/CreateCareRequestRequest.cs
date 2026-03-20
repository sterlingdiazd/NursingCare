using System.ComponentModel.DataAnnotations;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed class CreateCareRequestRequest
{
    [Required]
    [MaxLength(1000)]
    public string CareRequestDescription { get; set; } = default!;

    [Required]
    public string CareRequestType { get; set; } = default!;

    public Guid? NurseId { get; set; }
    public string? SuggestedNurse { get; set; }
    public string? AssignedNurse { get; set; }

    public int Unit { get; set; } = 1;
    public decimal? Price { get; set; }
    public decimal? ClientBasePriceOverride { get; set; }

    public string? DistanceFactor { get; set; }
    public string? ComplexityLevel { get; set; }

    public decimal? MedicalSuppliesCost { get; set; }
    public DateOnly? CareRequestDate { get; set; }
}

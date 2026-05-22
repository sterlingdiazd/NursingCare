using System.ComponentModel.DataAnnotations;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed class CreateCareRequestRequest
{
    [Required]
    [MaxLength(1000)]
    public string CareRequestDescription { get; set; } = default!;

    [Required]
    public string CareRequestType { get; set; } = default!;

    /// <summary>
    /// Optional client user id. When the caller is an ADMIN creating the
    /// request on behalf of a real client, this MUST be set to the client's
    /// user id. When the caller is a CLIENT creating their own request,
    /// leave this null and the backend will use the JWT subject.
    /// </summary>
    public Guid? ClientUserId { get; set; }

    public string? SuggestedNurse { get; set; }

    public int Unit { get; set; } = 1;
    public decimal? Price { get; set; }
    public decimal? ClientBasePriceOverride { get; set; }

    public string? DistanceFactor { get; set; }
    public string? ComplexityLevel { get; set; }

    public decimal? MedicalSuppliesCost { get; set; }
    public DateOnly? CareRequestDate { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed class CreateCareRequestRequest
{
    [Required]
    public Guid ResidentId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = default!;
}

using System.ComponentModel.DataAnnotations;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed class AssignCareRequestNurseRequest
{
    [Required]
    public Guid AssignedNurse { get; set; }
}

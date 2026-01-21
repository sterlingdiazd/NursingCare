namespace NursingCare.Api.Controllers.CareRequests;

public sealed class CreateCareRequestRequest
{
    public Guid ResidentId { get; set; }
    public string Description { get; set; } = default!;
}

namespace NursingCare.Application.CareRequests.Commands.CreateCareRequest;

public class CreateCareRequestCommand
{
  public Guid ResidentId { get; init; }
  public string Description { get; init; } = default!;
}

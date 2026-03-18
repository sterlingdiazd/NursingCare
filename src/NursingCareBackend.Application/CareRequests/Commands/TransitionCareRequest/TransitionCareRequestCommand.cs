namespace NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;

public sealed record TransitionCareRequestCommand(
  Guid CareRequestId,
  CareRequestTransitionAction Action
);

public enum CareRequestTransitionAction
{
  Approve = 0,
  Reject = 1,
  Complete = 2
}

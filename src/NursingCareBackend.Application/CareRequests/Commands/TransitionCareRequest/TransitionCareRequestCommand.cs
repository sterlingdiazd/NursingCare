namespace NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;

public sealed record TransitionCareRequestCommand(
  Guid CareRequestId,
  CareRequestTransitionAction Action,
  Guid? ActingUserId = null,
  string? Reason = null
);

public enum CareRequestTransitionAction
{
  Approve = 0,
  Reject = 1,
  Complete = 2,
  Cancel = 3
}

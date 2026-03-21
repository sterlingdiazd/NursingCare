using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;

public sealed class TransitionCareRequestHandler
{
  private readonly ICareRequestRepository _repository;

  public TransitionCareRequestHandler(ICareRequestRepository repository)
  {
    _repository = repository;
  }

  public async Task<CareRequest> Handle(
    TransitionCareRequestCommand command,
    CancellationToken cancellationToken)
  {
    var careRequest = await _repository.GetByIdAsync(command.CareRequestId, null, cancellationToken);

    if (careRequest is null)
    {
      throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
    }

    var transitionedAtUtc = DateTime.UtcNow;

    switch (command.Action)
    {
      case CareRequestTransitionAction.Approve:
        careRequest.Approve(transitionedAtUtc);
        break;
      case CareRequestTransitionAction.Reject:
        careRequest.Reject(transitionedAtUtc);
        break;
      case CareRequestTransitionAction.Complete:
        careRequest.Complete(transitionedAtUtc);
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(command.Action), command.Action, "Unsupported care request transition.");
    }

    await _repository.UpdateAsync(careRequest, cancellationToken);
    return careRequest;
  }
}

using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;

public sealed class TransitionCareRequestHandler
{
  private readonly ICareRequestRepository _repository;
  private readonly IAdminNotificationPublisher _notifications;

  public TransitionCareRequestHandler(
    ICareRequestRepository repository,
    IAdminNotificationPublisher notifications)
  {
    _repository = repository;
    _notifications = notifications;
  }

  public async Task<CareRequest> Handle(
    TransitionCareRequestCommand command,
    CancellationToken cancellationToken)
  {
    var careRequest = await _repository.GetByIdAsync(
      command.CareRequestId,
      CareRequestAccessScope.Admin,
      cancellationToken);

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
        if (!command.ActingUserId.HasValue || command.ActingUserId == Guid.Empty)
        {
          throw new InvalidOperationException("A valid nurse user identifier is required to complete a care request.");
        }

        careRequest.Complete(transitionedAtUtc, command.ActingUserId.Value);
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(command.Action), command.Action, "Unsupported care request transition.");
    }

    await _repository.UpdateAsync(careRequest, cancellationToken);

    if (command.Action == CareRequestTransitionAction.Reject)
    {
      await _notifications.PublishToAdminsAsync(
        new AdminNotificationPublishRequest(
          Category: "care_request_rejected",
          Severity: "High",
          Title: "Solicitud rechazada",
          Body: $"La solicitud \"{careRequest.Description}\" fue rechazada y requiere seguimiento comercial u operativo.",
          EntityType: "CareRequest",
          EntityId: careRequest.Id.ToString(),
          DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
          Source: "Administracion",
          RequiresAction: true),
        cancellationToken);
    }

    if (command.Action == CareRequestTransitionAction.Complete)
    {
      await _notifications.PublishToAdminsAsync(
        new AdminNotificationPublishRequest(
          Category: "care_request_completed",
          Severity: "Medium",
          Title: "Solicitud completada",
          Body: $"La solicitud \"{careRequest.Description}\" fue completada por la enfermera asignada.",
          EntityType: "CareRequest",
          EntityId: careRequest.Id.ToString(),
          DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
          Source: "Operacion de enfermeria",
          RequiresAction: false),
        cancellationToken);
    }

    return careRequest;
  }
}

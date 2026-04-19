using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Application.Payroll;

namespace NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;

public sealed class TransitionCareRequestHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IAdminNotificationPublisher _notifications;
    private readonly IPayrollCompensationService _payrollCompensationService;
    private readonly IAdminAuditService _auditService;

    public TransitionCareRequestHandler(
      ICareRequestRepository repository,
      IAdminNotificationPublisher notifications,
      IPayrollCompensationService payrollCompensationService,
      IAdminAuditService auditService)
    {
        _repository = repository;
        _notifications = notifications;
        _payrollCompensationService = payrollCompensationService;
        _auditService = auditService;
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
                careRequest.Reject(transitionedAtUtc, command.Reason);
                break;
            case CareRequestTransitionAction.Complete:
                if (!command.ActingUserId.HasValue || command.ActingUserId == Guid.Empty)
                {
                    throw new InvalidOperationException("A valid nurse user identifier is required to complete a care request.");
                }

                careRequest.Complete(transitionedAtUtc, command.ActingUserId.Value);
                break;
            case CareRequestTransitionAction.Cancel:
                careRequest.Cancel(transitionedAtUtc);
                break;
            case CareRequestTransitionAction.Invoice:
                careRequest.MarkAsInvoiced(command.InvoiceNumber!, transitionedAtUtc);
                break;
            case CareRequestTransitionAction.Pay:
                careRequest.RecordPayment(command.BankReference!, transitionedAtUtc);
                break;
            case CareRequestTransitionAction.Void:
                careRequest.VoidRequest(command.Reason!, transitionedAtUtc);
                break;
            case CareRequestTransitionAction.GenerateReceipt:
                careRequest.GenerateReceipt(transitionedAtUtc);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command.Action), command.Action, "Unsupported care request transition.");
        }

        await _repository.UpdateAsync(careRequest, cancellationToken);

        await _auditService.WriteAsync(
          new AdminAuditRecord(
            ActorUserId: command.ActingUserId,
            ActorRole: "System",
            Action: command.Action.ToString(),
            EntityType: "CareRequest",
            EntityId: careRequest.Id.ToString(),
            Notes: command.Action == CareRequestTransitionAction.Reject ? command.Reason : null,
            MetadataJson: null),
          cancellationToken);

        if (command.Action == CareRequestTransitionAction.Complete)
        {
            await _payrollCompensationService.RecordExecutionForCompletedCareRequestAsync(careRequest, cancellationToken);
        }

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

        if (command.Action == CareRequestTransitionAction.Cancel)
        {
            await _notifications.PublishToAdminsAsync(
              new AdminNotificationPublishRequest(
                Category: "care_request_cancelled",
                Severity: "Medium",
                Title: "Solicitud cancelada",
                Body: $"La solicitud \"{careRequest.Description}\" fue cancelada.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Cliente",
                RequiresAction: false),
              cancellationToken);
        }

        if (command.Action == CareRequestTransitionAction.Invoice)
        {
            await _notifications.PublishToAdminsAsync(
              new AdminNotificationPublishRequest(
                Category: "care_request_invoiced",
                Severity: "Low",
                Title: "Solicitud facturada",
                Body: $"La solicitud \"{careRequest.Description}\" fue facturada con número {careRequest.InvoiceNumber}.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Facturacion",
                RequiresAction: false),
              cancellationToken);
        }

        if (command.Action == CareRequestTransitionAction.Pay)
        {
            await _notifications.PublishToAdminsAsync(
              new AdminNotificationPublishRequest(
                Category: "care_request_paid",
                Severity: "Low",
                Title: "Pago registrado",
                Body: $"Se registró el pago de la solicitud \"{careRequest.Description}\".",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Facturacion",
                RequiresAction: false),
              cancellationToken);
        }

        if (command.Action == CareRequestTransitionAction.Void)
        {
            await _notifications.PublishToAdminsAsync(
              new AdminNotificationPublishRequest(
                Category: "care_request_voided",
                Severity: "High",
                Title: "Solicitud anulada",
                Body: $"La solicitud \"{careRequest.Description}\" fue anulada. Motivo: {careRequest.VoidReason}",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Facturacion",
                RequiresAction: true),
              cancellationToken);
        }

        return careRequest;
    }
}

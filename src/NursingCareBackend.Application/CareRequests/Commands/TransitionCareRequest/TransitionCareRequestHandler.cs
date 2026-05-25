using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Application.Payroll;

namespace NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;

public sealed class TransitionCareRequestHandler
{
  private readonly ICareRequestRepository _repository;
  private readonly IAdminNotificationPublisher _notifications;
  private readonly IUserNotificationPublisher _userNotifications;
  private readonly IPayrollCompensationService _payrollCompensationService;
  private readonly IAdminAuditService _auditService;
  private readonly IInvoiceNumberGenerator _invoiceNumbers;

  public TransitionCareRequestHandler(
    ICareRequestRepository repository,
    IAdminNotificationPublisher notifications,
    IUserNotificationPublisher userNotifications,
    IPayrollCompensationService payrollCompensationService,
    IAdminAuditService auditService,
    IInvoiceNumberGenerator invoiceNumbers)
  {
    _repository = repository;
    _notifications = notifications;
    _userNotifications = userNotifications;
    _payrollCompensationService = payrollCompensationService;
    _auditService = auditService;
    _invoiceNumbers = invoiceNumbers;
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

      // Auto-generate the invoice the moment the service is completed (Completed -> Invoiced),
      // so the client can pay and report the payment. Number scheme is configurable (FiscalOptions).
      var invoiceNumber = await _invoiceNumbers.NextAsync(transitionedAtUtc, cancellationToken);
      careRequest.Invoice(invoiceNumber, transitionedAtUtc);
      await _repository.UpdateAsync(careRequest, cancellationToken);

      await _auditService.WriteAsync(
        new AdminAuditRecord(
          ActorUserId: command.ActingUserId,
          ActorRole: "System",
          Action: "Invoice",
          EntityType: "CareRequest",
          EntityId: careRequest.Id.ToString(),
          Notes: $"Factura {invoiceNumber} generada automaticamente al completar el servicio.",
          MetadataJson: null),
        cancellationToken);
    }

    if (command.Action == CareRequestTransitionAction.Approve)
    {
      await _userNotifications.PublishToUserAsync(
        new UserNotificationPublishRequest(
          RecipientUserId: careRequest.UserID,
          Category: "care_request_approved",
          Severity: "Medium",
          Title: "Solicitud aprobada",
          Body: $"Tu solicitud \"{careRequest.Description}\" fue aprobada. Te avisaremos cuando el servicio sea completado.",
          EntityType: "CareRequest",
          EntityId: careRequest.Id.ToString(),
          DeepLinkPath: $"/care-requests/{careRequest.Id}",
          Source: "Solicitudes",
          RequiresAction: false),
        cancellationToken);
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

      var rejectionDetail = string.IsNullOrWhiteSpace(command.Reason)
        ? "Comunícate con el equipo de soporte si necesitas más información."
        : $"Motivo: {command.Reason}";
      await _userNotifications.PublishToUserAsync(
        new UserNotificationPublishRequest(
          RecipientUserId: careRequest.UserID,
          Category: "care_request_rejected",
          Severity: "High",
          Title: "Solicitud rechazada",
          Body: $"Tu solicitud \"{careRequest.Description}\" fue rechazada. {rejectionDetail}",
          EntityType: "CareRequest",
          EntityId: careRequest.Id.ToString(),
          DeepLinkPath: $"/care-requests/{careRequest.Id}",
          Source: "Solicitudes",
          RequiresAction: false),
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

      await _userNotifications.PublishToUserAsync(
        new UserNotificationPublishRequest(
          RecipientUserId: careRequest.UserID,
          Category: "care_request_invoiced",
          Severity: "High",
          Title: "Servicio completado y facturado",
          Body: $"Tu servicio \"{careRequest.Description}\" fue completado. La factura {careRequest.InvoiceNumber} está disponible para pago.",
          EntityType: "CareRequest",
          EntityId: careRequest.Id.ToString(),
          DeepLinkPath: $"/care-requests/{careRequest.Id}",
          Source: "Facturación",
          RequiresAction: true),
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

      await _userNotifications.PublishToUserAsync(
        new UserNotificationPublishRequest(
          RecipientUserId: careRequest.UserID,
          Category: "care_request_cancelled",
          Severity: "Medium",
          Title: "Solicitud cancelada",
          Body: $"Tu solicitud \"{careRequest.Description}\" fue cancelada.",
          EntityType: "CareRequest",
          EntityId: careRequest.Id.ToString(),
          DeepLinkPath: $"/care-requests/{careRequest.Id}",
          Source: "Solicitudes",
          RequiresAction: false),
        cancellationToken);
    }

    return careRequest;
  }
}

using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Notifications;

namespace NursingCareBackend.Application.CareRequests.Commands.RejectPaymentProof;

public sealed record RejectPaymentProofResponse(Guid Id, string Status);

public sealed class RejectPaymentProofHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IAdminAuditService _auditService;
    private readonly IUserNotificationPublisher _userNotifications;

    public RejectPaymentProofHandler(
        ICareRequestRepository repository,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications)
    {
        _repository = repository;
        _auditService = auditService;
        _userNotifications = userNotifications;
    }

    public async Task<RejectPaymentProofResponse> Handle(
        RejectPaymentProofCommand command,
        CancellationToken cancellationToken)
    {
        var careRequest = await _repository.GetByIdAsync(
            command.CareRequestId, CareRequestAccessScope.Admin, cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        careRequest.RejectPayment(command.Reason, DateTime.UtcNow);
        await _repository.UpdateAsync(careRequest, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.ActingAdminUserId,
                ActorRole: "Admin",
                Action: AdminAuditActions.RejectPaymentProof,
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: $"Comprobante rechazado (factura {careRequest.InvoiceNumber}). Motivo: {command.Reason}",
                MetadataJson: null),
            cancellationToken);

        await _userNotifications.PublishToUserAsync(
            new UserNotificationPublishRequest(
                RecipientUserId: careRequest.UserID,
                Category: "payment_rejected",
                Severity: "High",
                Title: "Comprobante rechazado",
                Body: $"Tu comprobante de la solicitud \"{careRequest.Description}\" fue rechazado. " +
                      $"Motivo: {command.Reason}. Por favor reporta el pago nuevamente.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/care-requests/{careRequest.Id}",
                Source: "Cobros",
                RequiresAction: true),
            cancellationToken);

        return new RejectPaymentProofResponse(careRequest.Id, careRequest.Status.ToString());
    }
}

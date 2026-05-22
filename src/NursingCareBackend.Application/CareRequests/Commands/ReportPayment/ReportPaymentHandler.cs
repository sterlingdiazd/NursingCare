using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.ReportPayment;

public sealed class ReportPaymentHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IPaymentProofRepository _proofs;
    private readonly IAdminNotificationPublisher _notifications;
    private readonly IAdminEmailNotifier _emailNotifier;
    private readonly IAdminAuditService _auditService;

    public ReportPaymentHandler(
        ICareRequestRepository repository,
        IPaymentProofRepository proofs,
        IAdminNotificationPublisher notifications,
        IAdminEmailNotifier emailNotifier,
        IAdminAuditService auditService)
    {
        _repository = repository;
        _proofs = proofs;
        _notifications = notifications;
        _emailNotifier = emailNotifier;
        _auditService = auditService;
    }

    public async Task<CareRequest> Handle(ReportPaymentCommand command, CancellationToken cancellationToken)
    {
        // Client scope enforces ownership: only the owning client resolves the request.
        var careRequest = await _repository.GetByIdAsync(
            command.CareRequestId,
            CareRequestAccessScope.ForClient(command.ActingUserId),
            cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        if (careRequest.Status != CareRequestStatus.Invoiced)
        {
            throw new InvalidOperationException(
                $"Solo se puede reportar el pago de una solicitud facturada. Estado actual: {careRequest.Status}.");
        }

        var now = DateTime.UtcNow;
        var proof = PaymentProof.Create(
            careRequest.Id, command.ImageContent, command.ContentType, command.Note, command.ActingUserId, now);
        await _proofs.AddAsync(proof, cancellationToken);

        careRequest.ReportPayment(proof.Id, now);
        await _repository.UpdateAsync(careRequest, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.ActingUserId,
                ActorRole: "Client",
                Action: "ReportPayment",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: $"Cliente reportó el pago de la factura {careRequest.InvoiceNumber} (comprobante {proof.Id}).",
                MetadataJson: null),
            cancellationToken);

        // Notify admins: in-app + push (via outbox) and email. Verification is required before Paid.
        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "payment_reported",
                Severity: "High",
                Title: "Pago reportado",
                Body: $"El cliente reportó el pago de la solicitud \"{careRequest.Description}\" " +
                      $"(factura {careRequest.InvoiceNumber}). Verifica el comprobante y confirma la recepción.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Cobros",
                RequiresAction: true),
            cancellationToken);

        var html =
            $"<h2>Pago reportado</h2>" +
            $"<p>El cliente reportó el pago de la solicitud <strong>{careRequest.Description}</strong>.</p>" +
            $"<p>Factura: {careRequest.InvoiceNumber}<br/>Monto: RD$ {careRequest.Total:N2}</p>" +
            $"<p>Revisa el comprobante en la app y confirma la recepción del dinero para marcarla como Pagada.</p>";
        await _emailNotifier.SendToAdminsAsync("Pago reportado — Sol y Luna", html, cancellationToken);

        return careRequest;
    }
}

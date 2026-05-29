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
            careRequest.Id, command.ImageContent, command.ContentType, command.Note, command.ActingUserId, now,
            claimedBankReference: command.ClaimedBankReference,
            claimedAmount: command.ClaimedAmount,
            claimedPaymentDate: command.ClaimedPaymentDate,
            payingBank: command.PayingBank,
            ocrDraftSentence: command.OcrDraftSentence,
            ocrExtractedBankReference: command.OcrExtractedBankReference,
            ocrExtractedAmount: command.OcrExtractedAmount,
            ocrExtractedPaymentDate: command.OcrExtractedPaymentDate,
            ocrExtractedBank: command.OcrExtractedBank,
            ocrConfidence: command.OcrConfidence,
            ocrWarningsJson: command.OcrWarningsJson,
            ocrProvider: command.OcrProvider,
            ocrAssessedAtUtc: command.OcrAssessedAtUtc,
            ocrClientEdited: command.OcrClientEdited);
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

        // Surface the structured claim so the admin can match it against the bank quickly, and flag
        // an amount that does not match the invoice. The image is a CLAIM — confirmation against the
        // bank is still required before Paid.
        var claimParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(proof.ClaimedBankReference))
            claimParts.Add($"Referencia: {proof.ClaimedBankReference}");
        if (proof.ClaimedAmount.HasValue)
        {
            var mismatch = !proof.AmountMatches(careRequest.Total) ? " (NO coincide con la factura)" : string.Empty;
            claimParts.Add($"Monto reportado: RD$ {proof.ClaimedAmount.Value:N2}{mismatch}");
        }
        if (proof.ClaimedPaymentDate.HasValue)
            claimParts.Add($"Fecha: {proof.ClaimedPaymentDate.Value:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(proof.PayingBank))
            claimParts.Add($"Banco: {proof.PayingBank}");
        var claimSummary = claimParts.Count > 0 ? " " + string.Join(". ", claimParts) + "." : string.Empty;

        // Notify admins: in-app + push (via outbox) and email. Verification is required before Paid.
        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "payment_reported",
                Severity: "High",
                Title: "Pago reportado",
                Body: $"El cliente reportó el pago de la solicitud \"{careRequest.Description}\" " +
                      $"(factura {careRequest.InvoiceNumber}).{claimSummary} Verifica el comprobante contra el banco y confirma la recepción.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Cobros",
                RequiresAction: true),
            cancellationToken);

        var claimHtml = claimParts.Count > 0
            ? $"<p>Datos reportados:<br/>{string.Join("<br/>", claimParts)}</p>"
            : string.Empty;
        var html =
            $"<h2>Pago reportado</h2>" +
            $"<p>El cliente reportó el pago de la solicitud <strong>{careRequest.Description}</strong>.</p>" +
            $"<p>Factura: {careRequest.InvoiceNumber}<br/>Monto facturado: RD$ {careRequest.Total:N2}</p>" +
            claimHtml +
            $"<p>Revisa el comprobante y verifica el ingreso en tu cuenta antes de marcarla como Pagada.</p>";
        await _emailNotifier.SendToAdminsAsync("Pago reportado — Sol y Luna", html, cancellationToken);

        return careRequest;
    }
}

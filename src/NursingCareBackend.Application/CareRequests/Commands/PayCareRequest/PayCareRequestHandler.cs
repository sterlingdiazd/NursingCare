using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.PayCareRequest;

public sealed record PaidCareRequestResponse(
    Guid Id,
    DateTime PaidAtUtc,
    decimal TotalAmount
);

public sealed class PayCareRequestHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IPaymentValidationRepository _paymentValidationRepository;
    private readonly IAdminAuditService _auditService;
    private readonly IUserNotificationPublisher _userNotifications;
    private readonly IGenerateReceiptHandler _generateReceipt;
    private readonly ILogger<PayCareRequestHandler> _logger;
    private readonly IInvoiceNumberGenerator _invoiceNumbers;

    public PayCareRequestHandler(
        ICareRequestRepository repository,
        IPaymentValidationRepository paymentValidationRepository,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications,
        IGenerateReceiptHandler generateReceipt,
        ILogger<PayCareRequestHandler> logger,
        IInvoiceNumberGenerator invoiceNumbers)
    {
        _repository = repository;
        _paymentValidationRepository = paymentValidationRepository;
        _auditService = auditService;
        _userNotifications = userNotifications;
        _generateReceipt = generateReceipt;
        _logger = logger;
        _invoiceNumbers = invoiceNumbers;
    }

    public async Task<PaidCareRequestResponse> Handle(
        PayCareRequestCommand command,
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

        // Anti-fraud: refuse to confirm a payment whose bank reference already confirmed a DIFFERENT
        // request — that would count one real transfer twice. The admin can override deliberately
        // (AcknowledgeDuplicateReference) for the rare case of one transfer covering several invoices.
        // We always detect it (even when acknowledging) so a forced override can be audited.
        var referenceInUse = await _paymentValidationRepository.IsBankReferenceUsedAsync(
            command.BankReference, careRequest.Id, cancellationToken);
        if (referenceInUse && !command.AcknowledgeDuplicateReference)
        {
            throw new InvalidOperationException(
                $"La referencia bancaria \"{command.BankReference}\" ya fue usada para confirmar otro pago. " +
                "Verifica que no estés registrando la misma transferencia dos veces.");
        }

        careRequest.Pay(command.BankReference, command.PaymentDate);

        await _repository.UpdateAsync(careRequest, cancellationToken);

        // Fiscal mode only: now that the money is confirmed received, emit the formal DGII e-NCF and
        // persist it on the request. This is the single point where a fiscal sequence number is
        // burned — completions and pre-payment voids never reach here, so the sequence has no gaps.
        // When fiscal mode is off (today's default) nothing extra happens: Ncf stays null.
        if (await _invoiceNumbers.IsFiscalModeEnabledAsync(cancellationToken) && careRequest.Ncf is null)
        {
            var ncf = await _invoiceNumbers.NextFiscalNcfAsync(command.PaymentDate, cancellationToken);
            careRequest.IssueFiscalReceipt(ncf, command.PaymentDate);
            await _repository.UpdateAsync(careRequest, cancellationToken);

            await _auditService.WriteAsync(
                new AdminAuditRecord(
                    ActorUserId: command.ActingAdminUserId,
                    ActorRole: "Admin",
                    Action: "IssueFiscalReceipt",
                    EntityType: "CareRequest",
                    EntityId: careRequest.Id.ToString(),
                    Notes: $"e-NCF {ncf} emitido al confirmar el pago.",
                    MetadataJson: null),
                cancellationToken);
        }

        var paymentValidation = PaymentValidation.Create(
            careRequestId: careRequest.Id,
            bankReference: command.BankReference,
            invoiceReference: careRequest.InvoiceNumber!,
            systemTotal: careRequest.Total,
            validatedByUserId: command.ActingAdminUserId,
            validatedAtUtc: command.PaymentDate);

        await _paymentValidationRepository.AddAsync(paymentValidation, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.ActingAdminUserId,
                ActorRole: "Admin",
                Action: "Pay",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: referenceInUse
                    ? $"Bank reference: {command.BankReference} — REFERENCIA DUPLICADA reconocida y forzada por el administrador."
                    : $"Bank reference: {command.BankReference}",
            MetadataJson: null),
            cancellationToken);

        // T2.1: auto-generate the client receipt on payment confirmation. It is idempotent (returns
        // the existing receipt if one exists) and only valid for a Paid request, which we just are.
        // Failure-isolated: a receipt/PDF error must NEVER fail the payment — the payment is the
        // source of truth and the manual "Generar recibo" action remains as a fallback. We only tell
        // the client the receipt is ready when it actually generated.
        var receiptReady = false;
        try
        {
            var receiptResult = await _generateReceipt.Handle(
                new GenerateReceiptCommand(careRequest.Id, command.ActingAdminUserId),
                cancellationToken);
            // Only claim the receipt is available if it actually produced content.
            receiptReady = !string.IsNullOrEmpty(receiptResult.ReceiptContentBase64);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Auto-receipt generation failed for care request {CareRequestId} after payment; payment stands, receipt can be generated manually.",
                careRequest.Id);
        }

        var notificationBody = receiptReady
            ? $"Confirmamos el pago de tu solicitud \"{careRequest.Description}\". Tu recibo ya está disponible."
            : $"Confirmamos el pago de tu solicitud \"{careRequest.Description}\".";

        // The client notification is a soft side-effect: like the receipt, a failure here must NOT
        // fail the already-committed payment (which would 500 the admin and tempt a retry that the
        // domain state guard then blocks). Isolate it.
        try
        {
            await _userNotifications.PublishToUserAsync(
                new UserNotificationPublishRequest(
                    RecipientUserId: careRequest.UserID,
                    Category: "payment_confirmed",
                    Severity: "Medium",
                    Title: "Pago confirmado",
                    Body: notificationBody,
                    EntityType: "CareRequest",
                    EntityId: careRequest.Id.ToString(),
                    DeepLinkPath: $"/care-requests/{careRequest.Id}",
                    Source: "Cobros",
                    RequiresAction: false),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Payment-confirmed notification failed for care request {CareRequestId}; payment stands.",
                careRequest.Id);
        }

        return new PaidCareRequestResponse(
            careRequest.Id,
            careRequest.PaidAtUtc!.Value,
            careRequest.Total);
    }
}

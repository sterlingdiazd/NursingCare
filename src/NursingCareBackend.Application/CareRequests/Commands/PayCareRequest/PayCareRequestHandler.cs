using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
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

    public PayCareRequestHandler(
        ICareRequestRepository repository,
        IPaymentValidationRepository paymentValidationRepository,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications)
    {
        _repository = repository;
        _paymentValidationRepository = paymentValidationRepository;
        _auditService = auditService;
        _userNotifications = userNotifications;
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

        careRequest.Pay(command.BankReference, command.PaymentDate);

        await _repository.UpdateAsync(careRequest, cancellationToken);

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
                Notes: $"Bank reference: {command.BankReference}",
            MetadataJson: null),
            cancellationToken);

        await _userNotifications.PublishToUserAsync(
            new UserNotificationPublishRequest(
                RecipientUserId: careRequest.UserID,
                Category: "payment_confirmed",
                Severity: "Medium",
                Title: "Pago confirmado",
                Body: $"Confirmamos el pago de tu solicitud \"{careRequest.Description}\". Gracias por usar NursingCare.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/care-requests/{careRequest.Id}",
                Source: "Cobros",
                RequiresAction: false),
            cancellationToken);

        return new PaidCareRequestResponse(
            careRequest.Id,
            careRequest.PaidAtUtc!.Value,
            careRequest.Total);
    }
}

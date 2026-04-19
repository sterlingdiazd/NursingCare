using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
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

    public PayCareRequestHandler(
        ICareRequestRepository repository,
        IPaymentValidationRepository paymentValidationRepository,
        IAdminAuditService auditService)
    {
        _repository = repository;
        _paymentValidationRepository = paymentValidationRepository;
        _auditService = auditService;
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

        return new PaidCareRequestResponse(
            careRequest.Id,
            careRequest.PaidAtUtc!.Value,
            careRequest.Total);
    }
}

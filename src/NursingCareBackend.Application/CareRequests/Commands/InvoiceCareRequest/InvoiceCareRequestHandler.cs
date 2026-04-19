using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.InvoiceCareRequest;

public sealed record InvoicedCareRequestResponse(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoicedAtUtc,
    decimal TotalAmount
);

public sealed class InvoiceCareRequestHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IAdminAuditService _auditService;

    public InvoiceCareRequestHandler(
        ICareRequestRepository repository,
        IAdminAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<InvoicedCareRequestResponse> Handle(
        InvoiceCareRequestCommand command,
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

        careRequest.Invoice(command.InvoiceNumber, command.InvoiceDate);

        await _repository.UpdateAsync(careRequest, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.ActingAdminUserId,
                ActorRole: "Admin",
                Action: "Invoice",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: $"Invoice number: {command.InvoiceNumber}",
                MetadataJson: null),
            cancellationToken);

        return new InvoicedCareRequestResponse(
            careRequest.Id,
            careRequest.InvoiceNumber!,
            careRequest.InvoicedAtUtc!.Value,
            careRequest.Total);
    }
}

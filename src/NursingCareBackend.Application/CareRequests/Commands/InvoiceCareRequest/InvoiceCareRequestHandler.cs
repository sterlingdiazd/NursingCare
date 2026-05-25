using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Notifications;
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
    private readonly IUserNotificationPublisher _userNotifications;

    public InvoiceCareRequestHandler(
        ICareRequestRepository repository,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications)
    {
        _repository = repository;
        _auditService = auditService;
        _userNotifications = userNotifications;
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

        await _userNotifications.PublishToUserAsync(
            new UserNotificationPublishRequest(
                RecipientUserId: careRequest.UserID,
                Category: "care_request_invoiced",
                Severity: "High",
                Title: "Factura disponible",
                Body: $"La factura {careRequest.InvoiceNumber} de tu solicitud \"{careRequest.Description}\" está disponible para pago.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/care-requests/{careRequest.Id}",
                Source: "Facturación",
                RequiresAction: true),
            cancellationToken);

        return new InvoicedCareRequestResponse(
            careRequest.Id,
            careRequest.InvoiceNumber!,
            careRequest.InvoicedAtUtc!.Value,
            careRequest.Total);
    }
}

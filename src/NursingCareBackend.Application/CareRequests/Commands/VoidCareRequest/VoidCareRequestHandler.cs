using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.VoidCareRequest;

public sealed record VoidedCareRequestResponse(
    Guid Id,
    DateTime VoidedAtUtc,
    string VoidReason
);

public sealed class VoidCareRequestHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IAdminAuditService _auditService;
    private readonly IUserNotificationPublisher _userNotifications;

    public VoidCareRequestHandler(
        ICareRequestRepository repository,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications)
    {
        _repository = repository;
        _auditService = auditService;
        _userNotifications = userNotifications;
    }

    public async Task<VoidedCareRequestResponse> Handle(
        VoidCareRequestCommand command,
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

        var voidedAtUtc = command.VoidedAtUtc;
        careRequest.Void(command.VoidReason, voidedAtUtc);

        await _repository.UpdateAsync(careRequest, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.ActingAdminUserId,
                ActorRole: "Admin",
                Action: "Void",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: command.VoidReason,
            MetadataJson: null),
            cancellationToken);

        await _userNotifications.PublishToUserAsync(
            new UserNotificationPublishRequest(
                RecipientUserId: careRequest.UserID,
                Category: "care_request_voided",
                Severity: "Medium",
                Title: "Solicitud anulada",
                Body: $"Tu solicitud \"{careRequest.Description}\" fue anulada. Motivo: {careRequest.VoidReason}",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/care-requests/{careRequest.Id}",
                Source: "Solicitudes",
                RequiresAction: false),
            cancellationToken);

        return new VoidedCareRequestResponse(
            careRequest.Id,
            careRequest.VoidedAtUtc!.Value,
            careRequest.VoidReason!);
    }
}

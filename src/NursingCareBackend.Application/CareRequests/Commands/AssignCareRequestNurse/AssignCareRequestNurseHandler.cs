using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;

public sealed class AssignCareRequestNurseHandler
{
    private readonly ICareRequestRepository _careRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminNotificationPublisher _notifications;

    public AssignCareRequestNurseHandler(
        ICareRequestRepository careRequestRepository,
        IUserRepository userRepository,
        IAdminNotificationPublisher notifications)
    {
        _careRequestRepository = careRequestRepository;
        _userRepository = userRepository;
        _notifications = notifications;
    }

    public async Task<Domain.CareRequests.CareRequest> Handle(
        AssignCareRequestNurseCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AssignedNurse == Guid.Empty)
        {
            throw new ArgumentException("Assigned nurse is required.", nameof(command.AssignedNurse));
        }

        var careRequest = await _careRequestRepository.GetByIdAsync(
            command.CareRequestId,
            CareRequestAccessScope.Admin,
            cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        var nurseUser = await _userRepository.GetByIdAsync(command.AssignedNurse, cancellationToken);
        if (nurseUser is null)
        {
            throw new InvalidOperationException("Assigned nurse was not found.");
        }

        if (nurseUser.ProfileType != UserProfileType.Nurse || nurseUser.NurseProfile is null)
        {
            throw new InvalidOperationException("Assigned user is not a nurse profile.");
        }

        if (!nurseUser.IsActive || !nurseUser.NurseProfile.IsActive)
        {
            throw new InvalidOperationException("Assigned nurse must have a completed active profile.");
        }

        var previousAssignedNurse = careRequest.AssignedNurse;
        careRequest.AssignNurse(command.AssignedNurse, DateTime.UtcNow);
        await _careRequestRepository.UpdateAsync(careRequest, cancellationToken);

        if (previousAssignedNurse.HasValue && previousAssignedNurse.Value != command.AssignedNurse)
        {
            await _notifications.PublishToAdminsAsync(
                new AdminNotificationPublishRequest(
                    Category: "care_request_reassigned",
                    Severity: "Medium",
                    Title: "Solicitud reasignada",
                    Body: $"La solicitud \"{careRequest.Description}\" fue reasignada a otra enfermera.",
                    EntityType: "CareRequest",
                    EntityId: careRequest.Id.ToString(),
                    DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                    Source: "Administracion",
                    RequiresAction: true),
                cancellationToken);
        }

        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "care_request_pending_approval",
                Severity: "Medium",
                Title: "Solicitud lista para aprobacion",
                Body: $"La solicitud \"{careRequest.Description}\" ya tiene enfermera asignada y esta lista para decision administrativa.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Administracion",
                RequiresAction: true),
            cancellationToken);

        return careRequest;
    }
}

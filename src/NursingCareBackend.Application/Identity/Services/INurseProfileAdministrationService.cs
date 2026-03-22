using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Responses;

namespace NursingCareBackend.Application.Identity.Services;

public interface INurseProfileAdministrationService
{
    Task<IReadOnlyList<PendingNurseProfileResponse>> GetPendingNurseProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminNurseProfileSummaryResponse>> GetActiveNurseProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminNurseProfileSummaryResponse>> GetInactiveNurseProfilesAsync(CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> GetNurseProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> CreateNurseProfileAsync(
        AdminCreateNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> UpdateNurseProfileAsync(
        Guid userId,
        AdminUpdateNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> CompleteNurseProfileCreationAsync(
        Guid userId,
        AdminCompleteNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> SetOperationalAccessAsync(
        Guid userId,
        AdminSetNurseOperationalAccessRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

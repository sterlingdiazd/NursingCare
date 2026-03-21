using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Responses;

namespace NursingCareBackend.Application.Identity.Services;

public interface INurseProfileAdministrationService
{
    Task<IReadOnlyList<PendingNurseProfileResponse>> GetPendingNurseProfilesAsync(CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> GetNurseProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> CompleteNurseProfileCreationAsync(
        Guid userId,
        AdminCompleteNurseProfileRequest request,
        CancellationToken cancellationToken = default);
}

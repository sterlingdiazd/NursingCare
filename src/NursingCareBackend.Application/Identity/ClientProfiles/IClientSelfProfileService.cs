namespace NursingCareBackend.Application.Identity.ClientProfiles;

public interface IClientSelfProfileService
{
  Task<ClientSelfProfileResponse?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

  Task<ClientSelfProfileResponse> UpdateAsync(
    Guid userId,
    UpdateClientSelfProfileRequest request,
    CancellationToken cancellationToken = default);
}

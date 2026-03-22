namespace NursingCareBackend.Application.AdminPortal.Users;

public interface IAdminUserManagementService
{
  Task<AdminUserDetail> UpdateIdentityAsync(
    Guid userId,
    AdminUserIdentityUpdate request,
    CancellationToken cancellationToken = default);

  Task<AdminUserDetail> UpdateRolesAsync(
    Guid userId,
    IReadOnlyCollection<string> roleNames,
    Guid? actorUserId,
    CancellationToken cancellationToken = default);

  Task<AdminUserDetail> UpdateActiveStateAsync(
    Guid userId,
    bool isActive,
    Guid? actorUserId,
    CancellationToken cancellationToken = default);

  Task<AdminUserSessionInvalidationResult> InvalidateSessionsAsync(
    Guid userId,
    CancellationToken cancellationToken = default);
}

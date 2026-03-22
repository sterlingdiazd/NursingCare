namespace NursingCareBackend.Application.AdminPortal.Users;

public interface IAdminAccountProvisioningService
{
  Task<AdminUserDetail> CreateAsync(
    CreateAdminAccountRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default);
}

namespace NursingCareBackend.Application.AdminPortal.Users;

public interface IAdminUserManagementRepository
{
  Task<IReadOnlyList<AdminUserListItem>> GetListAsync(
    AdminUserListFilter filter,
    CancellationToken cancellationToken = default);

  Task<AdminUserDetail?> GetByIdAsync(
    Guid userId,
    CancellationToken cancellationToken = default);
}

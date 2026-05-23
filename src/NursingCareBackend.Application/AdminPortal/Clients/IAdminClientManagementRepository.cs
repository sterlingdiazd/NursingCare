namespace NursingCareBackend.Application.AdminPortal.Clients;

public interface IAdminClientManagementRepository
{
  Task<AdminClientListPage> GetListAsync(
    AdminClientListFilter filter,
    CancellationToken cancellationToken = default);

  Task<AdminClientDetail?> GetByIdAsync(
    Guid userId,
    CancellationToken cancellationToken = default);
}

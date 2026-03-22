namespace NursingCareBackend.Application.AdminPortal.Queries;

public interface IAdminCareRequestRepository
{
  Task<IReadOnlyList<AdminCareRequestListItem>> GetListAsync(
    AdminCareRequestListFilter filter,
    CancellationToken cancellationToken);

  Task<AdminCareRequestDetail?> GetByIdAsync(
    Guid careRequestId,
    CancellationToken cancellationToken);

  Task<IReadOnlyList<AdminCareRequestClientOption>> GetActiveClientOptionsAsync(
    string? search,
    CancellationToken cancellationToken);
}

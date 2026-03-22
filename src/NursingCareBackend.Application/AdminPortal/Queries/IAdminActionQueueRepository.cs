namespace NursingCareBackend.Application.AdminPortal.Queries;

public interface IAdminActionQueueRepository
{
  Task<IReadOnlyList<AdminActionQueueItem>> GetItemsAsync(CancellationToken cancellationToken);
}

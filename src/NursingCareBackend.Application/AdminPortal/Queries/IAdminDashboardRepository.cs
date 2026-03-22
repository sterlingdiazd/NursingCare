namespace NursingCareBackend.Application.AdminPortal.Queries;

public interface IAdminDashboardRepository
{
  Task<AdminDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}

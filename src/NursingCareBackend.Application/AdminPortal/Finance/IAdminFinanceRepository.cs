namespace NursingCareBackend.Application.AdminPortal.Finance;

public interface IAdminFinanceRepository
{
    Task<FinanceOverview> GetOverviewAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

namespace NursingCareBackend.Application.AdminPortal.Finance;

public interface IAdminFinanceRepository
{
    Task<FinanceOverview> GetOverviewAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Source-record detail for a metric (collected, pending, services, category, line, clients, nurses, loans). Null if metric unknown.</summary>
    Task<FinanceDetail?> GetDetailAsync(string metric, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

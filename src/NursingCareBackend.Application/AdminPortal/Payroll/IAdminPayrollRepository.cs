namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IAdminPayrollRepository
{
    Task<AdminPayrollPeriodListResult> GetPeriodsAsync(
        AdminPayrollPeriodListFilter filter,
        CancellationToken cancellationToken);

    Task<AdminPayrollPeriodDetail?> GetPeriodByIdAsync(
        Guid periodId,
        CancellationToken cancellationToken);

    Task<Guid> CreatePeriodAsync(
        DateOnly startDate,
        DateOnly endDate,
        DateOnly cutoffDate,
        DateOnly paymentDate,
        CancellationToken cancellationToken);

    Task<bool> ClosePeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken);  // returns false if not found

    Task<IReadOnlyList<AdminPayrollLineItem>> GetPeriodLinesAsync(
        Guid periodId,
        CancellationToken cancellationToken);
}

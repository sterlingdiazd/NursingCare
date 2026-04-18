using NursingCareBackend.Application.AdminPortal.Payroll;

namespace NursingCareBackend.Application.Payroll;

public interface INursePayrollRepository
{
    Task<AdminPayrollPeriodListResult> GetPeriodsAsync(
        AdminPayrollPeriodListFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminPayrollLineItem>> GetPeriodLinesAsync(
        Guid periodId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NursePeriodHistoryItem>> GetNursePeriodHistoryAsync(
        Guid nurseId, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<int> CountNurseLinesInOpenPeriodsAsync(Guid nurseId, CancellationToken cancellationToken);

    Task<int> CountNurseLinesInClosedPeriodsAsync(Guid nurseId, CancellationToken cancellationToken);

    Task<NursePeriodDetail?> GetNursePeriodDetailAsync(
        Guid periodId, Guid nurseId, CancellationToken cancellationToken);
}

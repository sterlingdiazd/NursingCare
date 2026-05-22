using System;
using System.Threading;
using System.Threading.Tasks;

namespace NursingCareBackend.Application.Payroll;

public interface IScheduledDeductionService
{
    /// <summary>
    /// Idempotently generates the due installment <c>DeductionRecord</c> for every active scheduled
    /// deduction across all open payroll periods. Safe to call after registering a plan, creating a
    /// period, or on demand.
    /// </summary>
    Task EnsureInstallmentsForOpenPeriodsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Recomputes settled progress for every plan that has an installment in the given period, from
    /// the authoritative closed-period records. Call when a period transitions to closed.
    /// </summary>
    Task SettlePeriodInstallmentsAsync(Guid periodId, CancellationToken cancellationToken);
}

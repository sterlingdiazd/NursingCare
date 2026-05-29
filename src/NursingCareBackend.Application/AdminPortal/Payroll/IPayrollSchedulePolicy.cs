using System;
using System.Threading;
using System.Threading.Tasks;

namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Authoritative payroll schedule policy, read live from owner-configurable SystemSettings
/// (PAYROLL_*) so adjustments take effect without a redeploy. There is ONE cutoff rule —
/// <c>cutoff = periodEnd − PAYROLL_CUTOFF_DAYS_BEFORE_END</c> — and both the cutoff-only helpers
/// and the full (cutoff, payment) computation derive the cutoff from it, so every consumer agrees
/// on a single source of truth. The payment date layers the configurable payment-date policy
/// (mode, fixed payment days per quincena) on top of that cutoff.
/// </summary>
public interface IPayrollSchedulePolicy
{
    /// <summary>Days before the period end at which the accounting cutoff falls
    /// (PAYROLL_CUTOFF_DAYS_BEFORE_END; default 2). Always &gt;= 0.</summary>
    Task<int> GetCutoffDaysBeforeEndAsync(CancellationToken cancellationToken = default);

    /// <summary>Cutoff date for a period ending on <paramref name="endDate"/>, i.e.
    /// <c>endDate − GetCutoffDaysBeforeEndAsync()</c>.</summary>
    Task<DateOnly> ResolveCutoffDateAsync(DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compute the authoritative (cutoff, payment) dates for a period's [start, end] range. The
    /// cutoff uses the rule above; the payment date follows the configurable payment-date policy.
    /// Reads the current settings on each call (cheap; cached for the call's duration).
    /// </summary>
    Task<(DateOnly Cutoff, DateOnly Payment)> ComputeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
}

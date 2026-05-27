namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Owner-configurable payroll schedule policy, read live from the editable SystemSettings (PAYROLL_*)
/// so adjustments take effect without a redeploy. Surfaces the cutoff offset used when a period's
/// cutoff date must be derived from its end date.
/// </summary>
public interface IPayrollSchedulePolicy
{
    /// <summary>Days before the period end at which the accounting cutoff falls
    /// (PAYROLL_CUTOFF_DAYS_BEFORE_END; default 2). Always &gt;= 0.</summary>
    Task<int> GetCutoffDaysBeforeEndAsync(CancellationToken cancellationToken = default);

    /// <summary>Cutoff date for a period ending on <paramref name="endDate"/>, i.e.
    /// <c>endDate - GetCutoffDaysBeforeEndAsync()</c>.</summary>
    Task<DateOnly> ResolveCutoffDateAsync(DateOnly endDate, CancellationToken cancellationToken = default);
}

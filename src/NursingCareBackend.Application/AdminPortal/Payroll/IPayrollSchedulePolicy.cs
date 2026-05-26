using System;
using System.Threading;
using System.Threading.Tasks;

namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Authoritative payroll payment-date policy. Given a period's [start, end] range it computes
/// the (cutoff, payment) pair that the period MUST use, reading the owner-configured policy from
/// system settings (mode, fixed payment days per quincena, days-before-month-end). This is the
/// single backend source of truth so that EVERY period — created via the API, seeded, or
/// backfilled — obeys the same rule, instead of the policy only living in the mobile prefill.
/// </summary>
public interface IPayrollSchedulePolicy
{
    /// <summary>
    /// Compute the authoritative (cutoff, payment) dates for a period's [start, end] range.
    /// Reads the current policy settings on each call (cheap; cached for the call's duration).
    /// </summary>
    Task<(DateOnly Cutoff, DateOnly Payment)> ComputeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
}

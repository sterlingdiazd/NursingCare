namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Pre-close advisory checks for a payroll period. These do not block closing outright,
/// but the caller must acknowledge them explicitly before the period is locked.
/// </summary>
/// <param name="NegativeNetNurses">Nurses whose net pay for the period is zero or negative
/// (rate not set, or deductions exceed gross).</param>
/// <param name="UnliquidatedServices">Completed service executions whose service date falls
/// inside the period window but that have no payroll line in this period (would go unpaid).</param>
public sealed record PeriodCloseWarnings(int NegativeNetNurses, int UnliquidatedServices)
{
    public bool HasWarnings => NegativeNetNurses > 0 || UnliquidatedServices > 0;
}

namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Outcome of an attempt to reopen a payroll period. Only a Closed period can be
/// reopened; reopening reverts it to Open and recomputes installment settlement.
/// </summary>
public enum PeriodReopenResult
{
    Success,
    NotFound,
    NotClosed,
}

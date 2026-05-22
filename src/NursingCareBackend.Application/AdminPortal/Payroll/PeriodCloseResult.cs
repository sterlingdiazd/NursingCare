namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Outcome of an attempt to close a payroll period. A period may only be closed
/// once it has payroll activity (at least one calculated line or one deduction);
/// closing an empty period with zero values is rejected.
/// </summary>
public enum PeriodCloseResult
{
    Success,
    NotFound,
    Empty,
}

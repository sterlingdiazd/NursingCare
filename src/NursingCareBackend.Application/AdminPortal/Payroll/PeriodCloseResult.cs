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

    /// <summary>
    /// The period has unacknowledged close warnings (unliquidated services or zero/negative
    /// net pay) evaluated AT CLOSE TIME, and the caller did not pass acknowledgeWarnings = true.
    /// The gate is re-checked inside the close path so it stays authoritative regardless of any
    /// stale preflight (TOCTOU-safe): data may change between the advisory request and the close.
    /// </summary>
    RequiresConfirmation,
}

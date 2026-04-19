namespace NursingCareBackend.Domain.Payroll;

/// <summary>
/// Thrown when a write operation is attempted on a closed payroll period.
/// </summary>
public sealed class PayrollPeriodClosedException : InvalidOperationException
{
    public PayrollPeriodClosedException()
        : base("Cannot modify a closed payroll period.") { }

    public PayrollPeriodClosedException(Guid periodId)
        : base($"Cannot modify payroll period '{periodId}' because it is closed.") { }
}

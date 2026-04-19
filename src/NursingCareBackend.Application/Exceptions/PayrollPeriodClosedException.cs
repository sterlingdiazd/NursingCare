namespace NursingCareBackend.Application.Exceptions;

public sealed class PayrollPeriodClosedException : InvalidOperationException
{
    public Guid PeriodId { get; }

    public PayrollPeriodClosedException(Guid periodId)
        : base($"Payroll period {periodId} is closed and cannot be modified.")
    {
        PeriodId = periodId;
    }
}

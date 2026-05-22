namespace NursingCareBackend.Domain.Payroll;

public sealed class PayrollPeriod
{
    public Guid Id { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateOnly CutoffDate { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public PayrollPeriodStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    private PayrollPeriod() { }

    public static PayrollPeriod Create(DateOnly startDate, DateOnly endDate, DateOnly cutoffDate, DateOnly paymentDate, DateTime createdAtUtc)
    {
        ValidateSchedule(startDate, endDate, cutoffDate, paymentDate);

        return new PayrollPeriod
        {
            Id = Guid.NewGuid(),
            StartDate = startDate,
            EndDate = endDate,
            CutoffDate = cutoffDate,
            PaymentDate = paymentDate,
            Status = PayrollPeriodStatus.Open,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public bool IsClosed => Status == PayrollPeriodStatus.Closed;

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public void UpdateSchedule(DateOnly startDate, DateOnly endDate, DateOnly cutoffDate, DateOnly paymentDate)
    {
        EnsureOpen();
        ValidateSchedule(startDate, endDate, cutoffDate, paymentDate);

        StartDate = startDate;
        EndDate = endDate;
        CutoffDate = cutoffDate;
        PaymentDate = paymentDate;
    }

    // Standard period date rules: a coherent timeline of start ≤ end, start ≤ cutoff,
    // and cutoff ≤ payment. Cutoff may fall inside the period (before end); payment is
    // settled on or after the cutoff.
    private static void ValidateSchedule(DateOnly startDate, DateOnly endDate, DateOnly cutoffDate, DateOnly paymentDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Payroll period end date must be on or after the start date.", nameof(endDate));
        }

        if (cutoffDate < startDate)
        {
            throw new ArgumentException("Payroll period cutoff date must be on or after the start date.", nameof(cutoffDate));
        }

        if (paymentDate < cutoffDate)
        {
            throw new ArgumentException("Payroll period payment date must be on or after the cutoff date.", nameof(paymentDate));
        }
    }

    public void EnsureOpen()
    {
        if (IsClosed)
        {
            throw new InvalidOperationException(
                $"Payroll period {Id} is closed and cannot be modified.");
        }
    }

    public void Close(DateTime closedAtUtc)
    {
        if (Status == PayrollPeriodStatus.Closed)
        {
            return;
        }

        Status = PayrollPeriodStatus.Closed;
        ClosedAtUtc = closedAtUtc;
    }
}

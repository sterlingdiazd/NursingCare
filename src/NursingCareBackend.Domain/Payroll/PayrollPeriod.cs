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
        if (endDate < startDate)
        {
            throw new ArgumentException("Payroll period end date must be on or after the start date.", nameof(endDate));
        }

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

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

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

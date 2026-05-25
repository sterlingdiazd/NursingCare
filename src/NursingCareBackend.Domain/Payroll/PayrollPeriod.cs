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

    // Reopen audit: a closed period may be reopened (admin-only, audited) to correct
    // errors. These track the most recent reopen plus a running count for the trail.
    public DateTime? ReopenedAtUtc { get; private set; }
    public string? ReopenReason { get; private set; }
    public Guid? ReopenedByUserId { get; private set; }
    public int ReopenCount { get; private set; }

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

    // Standard period date rules: a coherent timeline of start ≤ cutoff ≤ end, and
    // cutoff ≤ payment. The cutoff is the accounting close — it falls inside the period
    // (on or before the end), never after it. Payment is settled on or after the cutoff.
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

        if (cutoffDate > endDate)
        {
            throw new ArgumentException("Payroll period cutoff date must be on or before the end date.", nameof(cutoffDate));
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

    // Reopen a closed period for correction. Requires a non-empty reason for the audit
    // trail. Reverts the period to Open and clears the closure stamp so it can be edited,
    // recalculated and re-closed. Reopening an already-open period is invalid.
    public void Reopen(string reason, Guid? reopenedByUserId, DateTime reopenedAtUtc)
    {
        if (Status != PayrollPeriodStatus.Closed)
        {
            throw new InvalidOperationException(
                $"Payroll period {Id} is not closed and cannot be reopened.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reopen a payroll period.", nameof(reason));
        }

        Status = PayrollPeriodStatus.Open;
        ClosedAtUtc = null;
        ReopenedAtUtc = reopenedAtUtc;
        ReopenReason = reason.Trim();
        ReopenedByUserId = reopenedByUserId;
        ReopenCount += 1;
    }
}

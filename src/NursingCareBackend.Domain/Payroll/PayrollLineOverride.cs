namespace NursingCareBackend.Domain.Payroll;

public enum PayrollLineOverrideStatus
{
    Pending,
    Approved,
    Rejected,
}

public sealed class PayrollLineOverride
{
    public Guid Id { get; private set; }
    public Guid PayrollLineId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public decimal OverrideAmount { get; private set; }
    public string Reason { get; private set; } = default!;
    public PayrollLineOverrideStatus Status { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    private PayrollLineOverride() { }

    public static PayrollLineOverride Create(
        Guid payrollLineId,
        Guid requestedByUserId,
        decimal overrideAmount,
        string reason,
        DateTime requestedAtUtc)
    {
        if (payrollLineId == Guid.Empty)
            throw new ArgumentException("PayrollLine is required.", nameof(payrollLineId));
        if (requestedByUserId == Guid.Empty)
            throw new ArgumentException("Requesting user is required.", nameof(requestedByUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Override reason is required.", nameof(reason));

        return new PayrollLineOverride
        {
            Id = Guid.NewGuid(),
            PayrollLineId = payrollLineId,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = requestedAtUtc,
            OverrideAmount = decimal.Round(overrideAmount, 2, MidpointRounding.AwayFromZero),
            Reason = reason.Trim(),
            Status = PayrollLineOverrideStatus.Pending,
        };
    }

    public void Approve(Guid approvedByUserId, DateTime approvedAtUtc)
    {
        if (approvedByUserId == RequestedByUserId)
            throw new InvalidOperationException("The same admin who requested the override cannot approve it.");
        if (Status != PayrollLineOverrideStatus.Pending)
            throw new InvalidOperationException("Only a Pending override can be approved.");

        ApprovedByUserId = approvedByUserId;
        ResolvedAtUtc = approvedAtUtc;
        Status = PayrollLineOverrideStatus.Approved;
    }

    public void Reject(DateTime rejectedAtUtc)
    {
        if (Status != PayrollLineOverrideStatus.Pending)
            throw new InvalidOperationException("Only a Pending override can be rejected.");

        Status = PayrollLineOverrideStatus.Rejected;
        ResolvedAtUtc = rejectedAtUtc;
    }
}

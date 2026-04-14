namespace NursingCareBackend.Domain.Payroll;

public sealed class DeductionRecord
{
    public Guid Id { get; private set; }
    public Guid NurseUserId { get; private set; }
    public Guid? PayrollPeriodId { get; private set; }
    public DeductionType DeductionType { get; private set; }
    public string Label { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime EffectiveAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private DeductionRecord() { }

    public static DeductionRecord Create(
        Guid nurseUserId,
        Guid? payrollPeriodId,
        DeductionType deductionType,
        string label,
        decimal amount,
        string? notes,
        DateTime effectiveAtUtc,
        DateTime createdAtUtc)
    {
        if (nurseUserId == Guid.Empty)
        {
            throw new ArgumentException("Nurse user is required.", nameof(nurseUserId));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Deduction label is required.", nameof(label));
        }

        if (amount < 0)
        {
            throw new ArgumentException("Deduction amount must be >= 0.", nameof(amount));
        }

        return new DeductionRecord
        {
            Id = Guid.NewGuid(),
            NurseUserId = nurseUserId,
            PayrollPeriodId = payrollPeriodId,
            DeductionType = deductionType,
            Label = label.Trim(),
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            EffectiveAtUtc = effectiveAtUtc,
            CreatedAtUtc = createdAtUtc,
        };
    }
}

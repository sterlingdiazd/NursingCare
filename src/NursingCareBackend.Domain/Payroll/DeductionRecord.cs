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

    /// <summary>When set, this record is an auto-generated installment of a <see cref="ScheduledDeduction"/>.</summary>
    public Guid? ScheduledDeductionId { get; private set; }

    /// <summary>1-based installment number within the parent scheduled deduction, when applicable.</summary>
    public int? InstallmentSequence { get; private set; }

    /// <summary>Admin paused this installment for its (open) period: amount becomes 0 and is not
    /// collected. Reversible (Resume restores the amount). The record still counts as generated, so a
    /// paused amortizing installment is naturally recovered later, while a paused recurring-fixed one
    /// is simply skipped.</summary>
    public bool IsPaused { get; private set; }
    public decimal? AmountBeforePause { get; private set; }

    private DeductionRecord() { }

    public static DeductionRecord Create(
        Guid nurseUserId,
        Guid? payrollPeriodId,
        DeductionType deductionType,
        string label,
        decimal amount,
        string? notes,
        DateTime effectiveAtUtc,
        DateTime createdAtUtc,
        Guid? scheduledDeductionId = null,
        int? installmentSequence = null)
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
            ScheduledDeductionId = scheduledDeductionId,
            InstallmentSequence = installmentSequence,
        };
    }

    /// <summary>Zero a pending installment that an admin chose to skip for its period.</summary>
    public void MarkSkipped()
    {
        Amount = 0m;
        if (!Label.EndsWith(" (omitida)", StringComparison.Ordinal))
        {
            Label = $"{Label} (omitida)";
        }
    }

    /// <summary>Pause this installment for its open period: zero the amount but keep the record so it
    /// still counts as generated. Reversible via <see cref="Resume"/>.</summary>
    public void Pause()
    {
        if (IsPaused) return;
        AmountBeforePause = Amount;
        Amount = 0m;
        IsPaused = true;
        if (!Label.EndsWith(" (en pausa)", StringComparison.Ordinal))
        {
            Label = $"{Label} (en pausa)";
        }
    }

    /// <summary>Reverse a pause, restoring the original amount.</summary>
    public void Resume()
    {
        if (!IsPaused) return;
        Amount = AmountBeforePause ?? Amount;
        AmountBeforePause = null;
        IsPaused = false;
        if (Label.EndsWith(" (en pausa)", StringComparison.Ordinal))
        {
            Label = Label[..^" (en pausa)".Length];
        }
    }

    /// <summary>Edit a manual one-off deduction's concept, label and amount.</summary>
    public void Update(DeductionType deductionType, string label, decimal amount, string? notes)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Deduction label is required.", nameof(label));
        }

        if (amount < 0)
        {
            throw new ArgumentException("Deduction amount must be >= 0.", nameof(amount));
        }

        DeductionType = deductionType;
        Label = label.Trim();
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}

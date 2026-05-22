namespace NursingCareBackend.Domain.Payroll;

/// <summary>
/// A plan that auto-generates per-period <see cref="DeductionRecord"/> installments for a nurse.
/// Amortizing plans (loans/advances) carry a balance that closes when fully settled;
/// recurring-fixed plans (insurance/dues) charge a fixed amount until cancelled or ended.
/// </summary>
public sealed class ScheduledDeduction
{
    public Guid Id { get; private set; }
    public Guid NurseUserId { get; private set; }
    public DeductionType DeductionType { get; private set; }
    public string Label { get; private set; } = default!;
    public ScheduleModality Modality { get; private set; }
    public DeductionCadence Cadence { get; private set; }
    public ScheduledDeductionStatus Status { get; private set; }
    public DateOnly StartPeriodDate { get; private set; }
    public string? Notes { get; private set; }

    // Amortizing (loan/advance) — simple interest on principal.
    public decimal PrincipalAmount { get; private set; }
    public decimal InterestRatePercent { get; private set; }
    public decimal TotalRepayable { get; private set; }
    public decimal InstallmentAmount { get; private set; }
    public int TotalInstallments { get; private set; }

    // Recurring fixed (insurance/dues).
    public decimal RecurringAmount { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int? MaxOccurrences { get; private set; }

    // Progress (both modalities). "Generated" = a record exists; "Paid" = its period has closed.
    public int InstallmentsGenerated { get; private set; }
    public int InstallmentsPaid { get; private set; }
    public decimal AmountSettled { get; private set; }

    // Audit.
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancelReason { get; private set; }

    public decimal RemainingBalance => Modality == ScheduleModality.Amortizing
        ? Round(TotalRepayable - AmountSettled)
        : 0m;

    private ScheduledDeduction() { }

    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    // Round installments up to the cent so the regular installments cover the total in exactly
    // TotalInstallments steps (the final one absorbs the smaller remainder).
    private static decimal CeilingCents(decimal value)
        => decimal.Ceiling(value * 100m) / 100m;

    public static ScheduledDeduction CreateAmortizing(
        Guid nurseUserId,
        DeductionType deductionType,
        string label,
        decimal principalAmount,
        decimal interestRatePercent,
        int totalInstallments,
        DeductionCadence cadence,
        DateOnly startPeriodDate,
        string? notes,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        if (nurseUserId == Guid.Empty)
            throw new ArgumentException("Nurse user is required.", nameof(nurseUserId));
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required.", nameof(label));
        if (principalAmount <= 0)
            throw new ArgumentException("Principal must be greater than zero.", nameof(principalAmount));
        if (interestRatePercent < 0)
            throw new ArgumentException("Interest rate cannot be negative.", nameof(interestRatePercent));
        if (totalInstallments < 1)
            throw new ArgumentException("A loan needs at least one installment.", nameof(totalInstallments));

        var totalRepayable = Round(principalAmount * (1m + interestRatePercent / 100m));

        return new ScheduledDeduction
        {
            Id = Guid.NewGuid(),
            NurseUserId = nurseUserId,
            DeductionType = deductionType,
            Label = label.Trim(),
            Modality = ScheduleModality.Amortizing,
            Cadence = cadence,
            Status = ScheduledDeductionStatus.Active,
            StartPeriodDate = startPeriodDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PrincipalAmount = Round(principalAmount),
            InterestRatePercent = decimal.Round(interestRatePercent, 4, MidpointRounding.AwayFromZero),
            TotalRepayable = totalRepayable,
            InstallmentAmount = CeilingCents(totalRepayable / totalInstallments),
            TotalInstallments = totalInstallments,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    public static ScheduledDeduction CreateRecurring(
        Guid nurseUserId,
        DeductionType deductionType,
        string label,
        decimal recurringAmount,
        DeductionCadence cadence,
        DateOnly startPeriodDate,
        DateOnly? endDate,
        int? maxOccurrences,
        string? notes,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        if (nurseUserId == Guid.Empty)
            throw new ArgumentException("Nurse user is required.", nameof(nurseUserId));
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required.", nameof(label));
        if (recurringAmount <= 0)
            throw new ArgumentException("Recurring amount must be greater than zero.", nameof(recurringAmount));
        if (maxOccurrences is < 1)
            throw new ArgumentException("Max occurrences must be at least one when set.", nameof(maxOccurrences));
        if (endDate is { } e && e < startPeriodDate)
            throw new ArgumentException("End date cannot precede the start period.", nameof(endDate));

        return new ScheduledDeduction
        {
            Id = Guid.NewGuid(),
            NurseUserId = nurseUserId,
            DeductionType = deductionType,
            Label = label.Trim(),
            Modality = ScheduleModality.RecurringFixed,
            Cadence = cadence,
            Status = ScheduledDeductionStatus.Active,
            StartPeriodDate = startPeriodDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RecurringAmount = Round(recurringAmount),
            EndDate = endDate,
            MaxOccurrences = maxOccurrences,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    /// <summary>
    /// True when an installment is due for the given period. <paramref name="generatedCount"/> is the
    /// authoritative number of installment records that already exist for this plan.
    /// </summary>
    public bool AppliesToPeriod(DateOnly periodStart, DateOnly periodEnd, int generatedCount, decimal scheduledSoFar)
    {
        if (Status != ScheduledDeductionStatus.Active) return false;
        if (periodStart < StartPeriodDate) return false;

        // Monthly cadence only fires in the month-closing half (16-end).
        if (Cadence == DeductionCadence.Monthly && periodStart.Day < 16) return false;

        return Modality switch
        {
            // Amortizing keeps scheduling until the full repayable amount has been allotted, so a
            // skipped (zeroed) installment is naturally recovered by later periods.
            ScheduleModality.Amortizing => scheduledSoFar < TotalRepayable,
            ScheduleModality.RecurringFixed =>
                (MaxOccurrences is null || generatedCount < MaxOccurrences)
                && (EndDate is null || periodEnd <= EndDate),
            _ => false,
        };
    }

    /// <summary>Amount and 1-based sequence for the next installment, given prior progress.</summary>
    public (int Sequence, decimal Amount) NextInstallment(int generatedCount, decimal scheduledSoFar)
    {
        var sequence = generatedCount + 1;
        if (Modality == ScheduleModality.Amortizing)
        {
            // Cap the final installment at the outstanding balance so the schedule sums to TotalRepayable.
            var amount = Round(Math.Min(InstallmentAmount, TotalRepayable - scheduledSoFar));
            return (sequence, amount);
        }

        return (sequence, RecurringAmount);
    }

    /// <summary>Sync the cached generated-installment count with the authoritative record count.</summary>
    public void SyncGeneratedCount(int count) => InstallmentsGenerated = count;

    /// <summary>
    /// Set settled progress from the authoritative closed-period installment records. Idempotent —
    /// safe to call repeatedly — and auto-completes the plan once fully settled.
    /// </summary>
    public void ApplySettlement(int installmentsPaid, decimal amountSettled, DateTime now)
    {
        InstallmentsPaid = installmentsPaid;
        AmountSettled = Round(amountSettled);

        if (Status != ScheduledDeductionStatus.Active) return;

        var finished = Modality == ScheduleModality.Amortizing
            ? RemainingBalance <= 0m
            : MaxOccurrences is { } max && InstallmentsPaid >= max;

        if (finished)
        {
            Status = ScheduledDeductionStatus.Completed;
            ClosedAtUtc = now;
        }
    }

    /// <summary>Change the installment size of an amortizing plan; remaining balance is spread over new installments.</summary>
    public void RescheduleAmortizing(decimal newInstallmentAmount, DateTime now)
    {
        EnsureAmortizing();
        EnsureActive();
        if (newInstallmentAmount <= 0)
            throw new ArgumentException("Installment amount must be greater than zero.", nameof(newInstallmentAmount));

        var remaining = RemainingBalance;
        if (remaining <= 0m) return;

        var remainingCount = (int)Math.Ceiling(remaining / newInstallmentAmount);
        InstallmentAmount = newInstallmentAmount;
        TotalInstallments = InstallmentsPaid + remainingCount;
        // Realign the per-installment base so PeekNextInstallment's final-remainder math holds.
        TotalRepayable = Round(AmountSettled + remaining);
    }

    public void RescheduleRecurring(decimal? newAmount, DateOnly? newEndDate, int? newMaxOccurrences, DateTime now)
    {
        EnsureRecurring();
        EnsureActive();
        if (newAmount is { } a)
        {
            if (a <= 0) throw new ArgumentException("Recurring amount must be greater than zero.", nameof(newAmount));
            RecurringAmount = Round(a);
        }
        if (newMaxOccurrences is { } m)
        {
            if (m < InstallmentsGenerated)
                throw new InvalidOperationException("Max occurrences cannot be below installments already generated.");
            MaxOccurrences = m;
        }
        if (newEndDate is { } e)
        {
            if (e < StartPeriodDate) throw new ArgumentException("End date cannot precede the start period.", nameof(newEndDate));
            EndDate = e;
        }
    }

    /// <summary>Validate that an amortizing plan can be paid off early; returns the outstanding balance.</summary>
    public decimal EnsureEarlyPayoffAllowed()
    {
        EnsureAmortizing();
        EnsureActive();
        if (RemainingBalance <= 0m)
            throw new InvalidOperationException("El préstamo ya está saldado.");
        return RemainingBalance;
    }

    /// <summary>Account for a skipped installment so the displayed installment count stays sensible.</summary>
    public void RegisterSkip()
    {
        EnsureActive();
        if (Modality == ScheduleModality.Amortizing)
            TotalInstallments += 1;
    }

    public void Cancel(Guid cancelledByUserId, string reason, DateTime now)
    {
        if (Status == ScheduledDeductionStatus.Cancelled) return;
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));

        Status = ScheduledDeductionStatus.Cancelled;
        CancelledByUserId = cancelledByUserId;
        CancelReason = reason.Trim();
        ClosedAtUtc = now;
    }

    private void EnsureActive()
    {
        if (Status != ScheduledDeductionStatus.Active)
            throw new InvalidOperationException("Only an active scheduled deduction can be modified.");
    }

    private void EnsureAmortizing()
    {
        if (Modality != ScheduleModality.Amortizing)
            throw new InvalidOperationException("This operation only applies to amortizing (loan/advance) plans.");
    }

    private void EnsureRecurring()
    {
        if (Modality != ScheduleModality.RecurringFixed)
            throw new InvalidOperationException("This operation only applies to recurring fixed plans.");
    }
}

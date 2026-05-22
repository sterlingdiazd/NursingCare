namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed class CreateScheduledDeductionRequest
{
    public Guid NurseUserId { get; set; }
    public string DeductionType { get; set; } = "Loan";       // Loan | Advance | Insurance | Other
    public string Label { get; set; } = string.Empty;
    public string Modality { get; set; } = "Amortizing";       // Amortizing | RecurringFixed
    public string Cadence { get; set; } = "Monthly";           // Monthly | PerPeriod
    public DateOnly StartPeriodDate { get; set; }
    public string? Notes { get; set; }

    // Amortizing (loan/advance)
    public decimal? PrincipalAmount { get; set; }
    public decimal? InterestRatePercent { get; set; }
    public int? TotalInstallments { get; set; }

    // Recurring fixed (insurance/dues)
    public decimal? RecurringAmount { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? MaxOccurrences { get; set; }
}

public sealed class RescheduleScheduledDeductionRequest
{
    public decimal? InstallmentAmount { get; set; }   // amortizing
    public decimal? RecurringAmount { get; set; }     // recurring
    public DateOnly? EndDate { get; set; }            // recurring
    public int? MaxOccurrences { get; set; }          // recurring
}

public sealed class CancelScheduledDeductionRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class SkipInstallmentRequest
{
    public Guid PayrollPeriodId { get; set; }
}

public sealed record ScheduledDeductionListItem(
    Guid Id,
    Guid NurseUserId,
    string NurseDisplayName,
    string DeductionType,
    string Label,
    string Modality,
    string Cadence,
    string Status,
    DateOnly StartPeriodDate,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    decimal TotalRepayable,
    decimal InstallmentAmount,
    int TotalInstallments,
    decimal RecurringAmount,
    DateOnly? EndDate,
    int? MaxOccurrences,
    int InstallmentsGenerated,
    int InstallmentsPaid,
    decimal AmountSettled,
    decimal RemainingBalance,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc);

public sealed record ScheduledDeductionListResult(
    IReadOnlyList<ScheduledDeductionListItem> Items,
    int TotalCount);

public sealed record ScheduledDeductionInstallmentRow(
    int? Sequence,
    Guid? PayrollPeriodId,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    string Label,
    decimal Amount,
    bool Paid);

public sealed record ScheduledDeductionDetail(
    ScheduledDeductionListItem Plan,
    IReadOnlyList<ScheduledDeductionInstallmentRow> Installments);

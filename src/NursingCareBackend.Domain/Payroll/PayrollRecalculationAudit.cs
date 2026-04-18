namespace NursingCareBackend.Domain.Payroll;

public sealed class PayrollRecalculationAudit
{
    public Guid Id { get; private set; }
    public Guid TriggeredByUserId { get; private set; }
    public DateTime TriggeredAtUtc { get; private set; }
    public Guid? PeriodId { get; private set; }
    public Guid? RuleId { get; private set; }
    public int LinesAffected { get; private set; }
    public decimal TotalOldNet { get; private set; }
    public decimal TotalNewNet { get; private set; }

    private PayrollRecalculationAudit() { }

    public static PayrollRecalculationAudit Create(
        Guid triggeredByUserId,
        DateTime triggeredAtUtc,
        Guid? periodId,
        Guid? ruleId,
        int linesAffected,
        decimal totalOldNet,
        decimal totalNewNet)
    {
        if (triggeredByUserId == Guid.Empty)
            throw new ArgumentException("Triggered-by user is required.", nameof(triggeredByUserId));

        return new PayrollRecalculationAudit
        {
            Id = Guid.NewGuid(),
            TriggeredByUserId = triggeredByUserId,
            TriggeredAtUtc = triggeredAtUtc,
            PeriodId = periodId,
            RuleId = ruleId,
            LinesAffected = linesAffected,
            TotalOldNet = decimal.Round(totalOldNet, 2, MidpointRounding.AwayFromZero),
            TotalNewNet = decimal.Round(totalNewNet, 2, MidpointRounding.AwayFromZero),
        };
    }
}

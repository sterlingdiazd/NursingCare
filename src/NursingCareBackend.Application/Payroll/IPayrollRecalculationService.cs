namespace NursingCareBackend.Application.Payroll;

public sealed record RecalculatePayrollRequest(
    Guid? PeriodId,
    Guid? RuleId
);

public sealed record RecalculatePayrollResult(
    Guid AuditId,
    int LinesAffected,
    decimal TotalOldNet,
    decimal TotalNewNet,
    DateTime TriggeredAtUtc
);

public interface IPayrollRecalculationService
{
    Task<RecalculatePayrollResult> RecalculateAsync(
        Guid triggeredByUserId,
        RecalculatePayrollRequest request,
        CancellationToken cancellationToken);
}

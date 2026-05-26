namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.MarkNursePaymentFailed;

/// <summary>
/// Admin marks a nurse's confirmed/sent payment as FAILED at the bank (money did not reach the
/// nurse), with a required reason. Audited. Remediation: re-confirm to retry.
/// </summary>
public sealed record MarkNursePaymentFailedCommand(
    Guid PeriodId,
    Guid NurseUserId,
    Guid AdminUserId,
    string Reason);

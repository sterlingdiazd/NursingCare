namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ReverseNursePayment;

/// <summary>
/// Admin reverses a previously CONFIRMED nurse payment (with a required reason). Audited and the
/// nurse is notified. Remediation: re-confirm to re-pay against the corrected net.
/// </summary>
public sealed record ReverseNursePaymentCommand(
    Guid PeriodId,
    Guid NurseUserId,
    Guid AdminUserId,
    string Reason);

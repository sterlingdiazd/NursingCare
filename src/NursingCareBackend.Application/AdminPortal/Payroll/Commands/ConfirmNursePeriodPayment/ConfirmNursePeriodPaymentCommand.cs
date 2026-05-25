namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;

/// <summary>
/// Admin confirms that a nurse's bank transfer was made for a payroll period. On
/// confirmation the nurse's voucher PDF is generated and (for this demo) delivered to
/// the confirming admin's own email and phone.
/// </summary>
public sealed record ConfirmNursePeriodPaymentCommand(
    Guid PeriodId,
    Guid NurseUserId,
    Guid AdminUserId,
    string? BankReference);

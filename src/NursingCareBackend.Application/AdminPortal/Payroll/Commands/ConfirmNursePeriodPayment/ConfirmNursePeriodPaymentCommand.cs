namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;

/// <summary>
/// Admin confirms that a nurse's bank transfer was made for a payroll period. On
/// confirmation the nurse's voucher PDF is generated, validated, and emailed to the NURSE,
/// with the comprobante showing the payment as confirmed (PAGADO + bank reference + date).
/// </summary>
public sealed record ConfirmNursePeriodPaymentCommand(
    Guid PeriodId,
    Guid NurseUserId,
    Guid AdminUserId,
    string? BankReference);

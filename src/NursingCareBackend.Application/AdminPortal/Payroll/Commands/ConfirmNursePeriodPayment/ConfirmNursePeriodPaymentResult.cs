namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;

/// <summary>
/// Outcome of confirming a nurse's payment for a period. Field names map directly to the
/// (camelCase) JSON returned by the endpoint.
/// </summary>
public sealed record ConfirmNursePeriodPaymentResult(
    Guid PeriodId,
    Guid NurseUserId,
    DateTime ConfirmedAtUtc,
    string? BankReference,
    bool VoucherEmailSent,
    string WhatsappUrl,
    string RecipientLabel);

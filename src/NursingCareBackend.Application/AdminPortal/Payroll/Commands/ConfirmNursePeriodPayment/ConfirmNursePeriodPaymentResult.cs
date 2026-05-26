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
    string RecipientLabel,
    string? VoucherDeliveryDetail,
    // Real payment state (Pending/SentToBank/Confirmed/Failed/Reversed). Optional/additive: older
    // mobile builds ignore it. A confirm sets it to Confirmed.
    string? PaymentStatus = null);

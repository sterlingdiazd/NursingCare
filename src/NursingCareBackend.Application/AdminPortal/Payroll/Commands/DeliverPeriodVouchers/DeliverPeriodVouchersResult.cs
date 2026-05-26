namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.DeliverPeriodVouchers;

/// <summary>
/// Outcome of a batch comprobante delivery for a period. Field names map directly to the
/// (camelCase) JSON returned by the endpoint.
/// </summary>
public sealed record DeliverPeriodVouchersResult(
    Guid PeriodId,
    DateTime ConfirmedAtUtc,
    int TotalNurses,
    int DeliveredCount,
    int FailedCount,
    IReadOnlyList<DeliverPeriodVoucherItem> Items);

/// <summary>
/// Per-nurse result inside a batch delivery. <see cref="WhatsappUrl"/> is a wa.me deep link the
/// admin taps (wa.me cannot bulk-send); email is delivered automatically when valid.
/// </summary>
public sealed record DeliverPeriodVoucherItem(
    Guid NurseUserId,
    string NurseDisplayName,
    bool VoucherEmailSent,
    string WhatsappUrl,
    string RecipientLabel,
    string? VoucherDeliveryDetail);

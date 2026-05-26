namespace NursingCareBackend.Domain.Payroll;

/// <summary>
/// Real payment state of a nurse's per-period payout. SEPARATE from
/// <see cref="VoucherDeliveryStatus"/>, which only tracks whether the comprobante PDF email was
/// delivered. A delivery failure is NOT a payment failure, and vice versa. Persisted as a string.
/// </summary>
public enum NursePaymentStatus
{
    /// <summary>Pendiente — the nurse has payroll lines but the payment is not (yet) confirmed.</summary>
    Pending = 0,

    /// <summary>Enviado al banco — transfer issued, awaiting confirmation it landed (optional intermediate).</summary>
    SentToBank = 1,

    /// <summary>Confirmado — money out and confirmed; the comprobante is stamped PAGADO.</summary>
    Confirmed = 2,

    /// <summary>Fallido — a transfer attempt failed at the bank (money did NOT reach the nurse).</summary>
    Failed = 3,

    /// <summary>Revertido — a previously confirmed payment was reversed, with a reason.</summary>
    Reversed = 4,
}

namespace NursingCareBackend.Domain.Payroll;

/// <summary>
/// Records that an admin confirmed a nurse's payment (bank transfer) for a given
/// payroll period, and tracks the delivery of that nurse's voucher PDF.
///
/// One record per (period, nurse). Re-confirming the same pair updates the existing
/// record (idempotent) rather than creating a duplicate — a unique index on
/// (PayrollPeriodId, NurseUserId) enforces this at the database level.
/// </summary>
public sealed class NursePeriodPayment
{
    public Guid Id { get; private set; }
    public Guid PayrollPeriodId { get; private set; }
    public Guid NurseUserId { get; private set; }
    public DateTime ConfirmedAtUtc { get; private set; }
    public Guid ConfirmedByUserId { get; private set; }
    public string? BankReference { get; private set; }

    /// <summary>Real payment state (money), separate from voucher delivery. See <see cref="NursePaymentStatus"/>.</summary>
    public NursePaymentStatus PaymentStatus { get; private set; }
    /// <summary>Reason captured when the payment is marked failed or reversed.</summary>
    public string? PaymentStatusReason { get; private set; }
    /// <summary>When the payment status last changed, and by whom (for audit/traceability).</summary>
    public DateTime? StatusChangedAtUtc { get; private set; }
    public Guid? StatusChangedByUserId { get; private set; }

    /// <summary>Voucher delivery state: Pending, Sent or Failed.</summary>
    public VoucherDeliveryStatus VoucherDeliveryStatus { get; private set; }
    public DateTime? VoucherDeliveredAtUtc { get; private set; }
    public string? DeliveryError { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private NursePeriodPayment() { }

    public static NursePeriodPayment Create(
        Guid payrollPeriodId,
        Guid nurseUserId,
        Guid confirmedByUserId,
        string? bankReference,
        DateTime confirmedAtUtc)
    {
        if (payrollPeriodId == Guid.Empty)
        {
            throw new ArgumentException("Payroll period is required.", nameof(payrollPeriodId));
        }

        if (nurseUserId == Guid.Empty)
        {
            throw new ArgumentException("Nurse user is required.", nameof(nurseUserId));
        }

        if (confirmedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Confirming admin user is required.", nameof(confirmedByUserId));
        }

        return new NursePeriodPayment
        {
            Id = Guid.NewGuid(),
            PayrollPeriodId = payrollPeriodId,
            NurseUserId = nurseUserId,
            ConfirmedByUserId = confirmedByUserId,
            BankReference = NormalizeReference(bankReference),
            PaymentStatus = NursePaymentStatus.Confirmed,
            StatusChangedAtUtc = confirmedAtUtc,
            StatusChangedByUserId = confirmedByUserId,
            VoucherDeliveryStatus = VoucherDeliveryStatus.Pending,
            ConfirmedAtUtc = confirmedAtUtc,
            CreatedAtUtc = confirmedAtUtc,
            UpdatedAtUtc = confirmedAtUtc,
        };
    }

    /// <summary>
    /// Re-confirm an existing payment (idempotent path): refresh who/when confirmed and
    /// the bank reference, and reset delivery state so the voucher is re-sent.
    /// </summary>
    public void Reconfirm(Guid confirmedByUserId, string? bankReference, DateTime confirmedAtUtc)
    {
        if (confirmedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Confirming admin user is required.", nameof(confirmedByUserId));
        }

        ConfirmedByUserId = confirmedByUserId;
        BankReference = NormalizeReference(bankReference);
        ConfirmedAtUtc = confirmedAtUtc;
        // A (re)confirmation means the admin asserts the payment was made and landed.
        PaymentStatus = NursePaymentStatus.Confirmed;
        PaymentStatusReason = null;
        StatusChangedAtUtc = confirmedAtUtc;
        StatusChangedByUserId = confirmedByUserId;
        VoucherDeliveryStatus = VoucherDeliveryStatus.Pending;
        VoucherDeliveredAtUtc = null;
        DeliveryError = null;
        UpdatedAtUtc = confirmedAtUtc;
    }

    /// <summary>
    /// Mark a payment as failed at the bank (with a reason). Only valid from Confirmed or
    /// SentToBank — money was issued but did not reach the nurse. Idempotent on Failed.
    /// </summary>
    public void MarkPaymentFailed(string reason, Guid byUserId, DateTime atUtc)
    {
        // Idempotent: re-marking an already-failed payment is a no-op so a retry/double-submit does
        // not overwrite the original failure reason/actor/timestamp.
        if (PaymentStatus == NursePaymentStatus.Failed)
        {
            return;
        }
        if (PaymentStatus is not (NursePaymentStatus.Confirmed or NursePaymentStatus.SentToBank))
        {
            throw new InvalidOperationException("Solo se puede marcar como fallido un pago confirmado o enviado al banco.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("El motivo es requerido.", nameof(reason));
        }
        if (byUserId == Guid.Empty)
        {
            throw new ArgumentException("El usuario administrador es requerido.", nameof(byUserId));
        }

        PaymentStatus = NursePaymentStatus.Failed;
        PaymentStatusReason = reason.Trim();
        StatusChangedAtUtc = atUtc;
        StatusChangedByUserId = byUserId;
        UpdatedAtUtc = atUtc;
    }

    /// <summary>Reverse a previously confirmed payment (with a reason). Only valid from Confirmed.</summary>
    public void Reverse(string reason, Guid byUserId, DateTime atUtc)
    {
        if (PaymentStatus != NursePaymentStatus.Confirmed)
        {
            throw new InvalidOperationException("Solo se puede revertir un pago confirmado.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("El motivo es requerido.", nameof(reason));
        }
        if (byUserId == Guid.Empty)
        {
            throw new ArgumentException("El usuario administrador es requerido.", nameof(byUserId));
        }

        PaymentStatus = NursePaymentStatus.Reversed;
        PaymentStatusReason = reason.Trim();
        StatusChangedAtUtc = atUtc;
        StatusChangedByUserId = byUserId;
        UpdatedAtUtc = atUtc;
    }

    /// <summary>
    /// When the period is reopened, a previously confirmed/sent payment becomes stale (the net may
    /// change). Reset it to Pending so the admin must re-confirm against the corrected net. Preserves
    /// BankReference. Idempotent: a no-op unless currently Confirmed or SentToBank.
    /// </summary>
    public void ResetForReopen(Guid byUserId, DateTime atUtc)
    {
        if (PaymentStatus is not (NursePaymentStatus.Confirmed or NursePaymentStatus.SentToBank))
        {
            return;
        }

        PaymentStatus = NursePaymentStatus.Pending;
        StatusChangedAtUtc = atUtc;
        StatusChangedByUserId = byUserId == Guid.Empty ? StatusChangedByUserId : byUserId;
        UpdatedAtUtc = atUtc;
    }

    /// <summary>Mark the voucher as delivered (best-effort email succeeded).</summary>
    public void MarkVoucherDelivered(DateTime deliveredAtUtc)
    {
        VoucherDeliveryStatus = VoucherDeliveryStatus.Sent;
        VoucherDeliveredAtUtc = deliveredAtUtc;
        DeliveryError = null;
        UpdatedAtUtc = deliveredAtUtc;
    }

    /// <summary>Record a delivery failure without throwing — delivery is best-effort for the demo.</summary>
    public void MarkVoucherFailed(string? error, DateTime failedAtUtc)
    {
        VoucherDeliveryStatus = VoucherDeliveryStatus.Failed;
        VoucherDeliveredAtUtc = null;
        DeliveryError = string.IsNullOrWhiteSpace(error) ? "Error desconocido al enviar el comprobante." : error.Trim();
        UpdatedAtUtc = failedAtUtc;
    }

    private static string? NormalizeReference(string? bankReference) =>
        string.IsNullOrWhiteSpace(bankReference) ? null : bankReference.Trim();
}

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
        VoucherDeliveryStatus = VoucherDeliveryStatus.Pending;
        VoucherDeliveredAtUtc = null;
        DeliveryError = null;
        UpdatedAtUtc = confirmedAtUtc;
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

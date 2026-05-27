namespace NursingCareBackend.Domain.CareRequests;

/// <summary>
/// Image (invoice photo or bank-transfer screenshot) the client uploads to report a payment, plus
/// the STRUCTURED CLAIM around it (bank reference, amount, date, paying bank). Stored as bytes in the
/// database, mirroring the <see cref="Receipt"/> pattern.
///
/// The image is an unverified CLAIM, never proof of funds (a screenshot can be reused, edited, or
/// AI-generated). The structured claim is what enables real anti-fraud checks: a bank reference can
/// be matched against the owner's account and detected if reused; the amount can be matched to the
/// invoice. The care request only becomes Paid when the admin confirms the money landed in the bank
/// — the upload never auto-confirms.
/// </summary>
public sealed class PaymentProof
{
    public Guid Id { get; private set; }
    public Guid CareRequestId { get; private set; }
    public byte[] Content { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public string? Note { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    // Structured claim (anti-fraud). Nullable for back-compat with proofs uploaded before capture.
    public string? ClaimedBankReference { get; private set; }
    public decimal? ClaimedAmount { get; private set; }
    public DateOnly? ClaimedPaymentDate { get; private set; }
    public string? PayingBank { get; private set; }

    private PaymentProof() { } // For ORM

    public static PaymentProof Create(
        Guid careRequestId,
        byte[] content,
        string contentType,
        string? note,
        Guid uploadedByUserId,
        DateTime uploadedAtUtc,
        string? claimedBankReference = null,
        decimal? claimedAmount = null,
        DateOnly? claimedPaymentDate = null,
        string? payingBank = null)
    {
        if (careRequestId == Guid.Empty)
            throw new ArgumentException("CareRequestId cannot be empty.", nameof(careRequestId));

        if (content is null || content.Length == 0)
            throw new ArgumentException("Payment proof content cannot be empty.", nameof(content));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Payment proof content type cannot be empty.", nameof(contentType));

        if (uploadedByUserId == Guid.Empty)
            throw new ArgumentException("UploadedByUserId cannot be empty.", nameof(uploadedByUserId));

        if (claimedAmount is <= 0m)
            throw new ArgumentException("Claimed amount must be positive when provided.", nameof(claimedAmount));

        return new PaymentProof
        {
            Id = Guid.NewGuid(),
            CareRequestId = careRequestId,
            Content = content,
            ContentType = contentType,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            UploadedByUserId = uploadedByUserId,
            UploadedAtUtc = uploadedAtUtc,
            ClaimedBankReference = string.IsNullOrWhiteSpace(claimedBankReference) ? null : claimedBankReference.Trim(),
            ClaimedAmount = claimedAmount,
            ClaimedPaymentDate = claimedPaymentDate,
            PayingBank = string.IsNullOrWhiteSpace(payingBank) ? null : payingBank.Trim(),
        };
    }

    /// <summary>True when the client's claimed amount exactly matches the invoice total. False if it
    /// mismatches OR was not provided (admin should still match it manually against the bank).</summary>
    public bool AmountMatches(decimal invoiceTotal) => ClaimedAmount.HasValue && ClaimedAmount.Value == invoiceTotal;
}

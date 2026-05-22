namespace NursingCareBackend.Domain.CareRequests;

/// <summary>
/// Image (invoice photo or bank-transfer screenshot) the client uploads to report a payment.
/// Stored as bytes in the database, mirroring the <see cref="Receipt"/> pattern. The admin reviews
/// it and confirms the payment was received before the care request becomes Paid.
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

    private PaymentProof() { } // For ORM

    public static PaymentProof Create(
        Guid careRequestId,
        byte[] content,
        string contentType,
        string? note,
        Guid uploadedByUserId,
        DateTime uploadedAtUtc)
    {
        if (careRequestId == Guid.Empty)
            throw new ArgumentException("CareRequestId cannot be empty.", nameof(careRequestId));

        if (content is null || content.Length == 0)
            throw new ArgumentException("Payment proof content cannot be empty.", nameof(content));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Payment proof content type cannot be empty.", nameof(contentType));

        if (uploadedByUserId == Guid.Empty)
            throw new ArgumentException("UploadedByUserId cannot be empty.", nameof(uploadedByUserId));

        return new PaymentProof
        {
            Id = Guid.NewGuid(),
            CareRequestId = careRequestId,
            Content = content,
            ContentType = contentType,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            UploadedByUserId = uploadedByUserId,
            UploadedAtUtc = uploadedAtUtc,
        };
    }
}

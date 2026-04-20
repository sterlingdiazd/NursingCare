namespace NursingCareBackend.Domain.CareRequests;

public sealed class Receipt
{
    public Guid Id { get; private set; }
    public Guid CareRequestId { get; private set; }
    public string ReceiptNumber { get; private set; } = default!;
    public byte[] ReceiptContent { get; private set; } = default!;
    public DateTime GeneratedAtUtc { get; private set; }
    public Guid GeneratedByUserId { get; private set; }

    private Receipt() { } // For ORM

    public static Receipt Create(
        Guid careRequestId,
        string receiptNumber,
        byte[] receiptContent,
        Guid generatedByUserId,
        DateTime generatedAtUtc)
    {
        if (careRequestId == Guid.Empty)
            throw new ArgumentException("CareRequestId cannot be empty.", nameof(careRequestId));

        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new ArgumentException("ReceiptNumber cannot be empty.", nameof(receiptNumber));

        if (receiptContent is null || receiptContent.Length == 0)
            throw new ArgumentException("ReceiptContent cannot be empty.", nameof(receiptContent));

        if (generatedByUserId == Guid.Empty)
            throw new ArgumentException("GeneratedByUserId cannot be empty.", nameof(generatedByUserId));

        return new Receipt
        {
            Id = Guid.NewGuid(),
            CareRequestId = careRequestId,
            ReceiptNumber = receiptNumber,
            ReceiptContent = receiptContent,
            GeneratedByUserId = generatedByUserId,
            GeneratedAtUtc = generatedAtUtc,
        };
    }
}

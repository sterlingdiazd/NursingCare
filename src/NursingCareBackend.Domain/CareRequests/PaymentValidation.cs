namespace NursingCareBackend.Domain.CareRequests;

public sealed class PaymentValidation
{
    public Guid Id { get; private set; }
    public Guid CareRequestId { get; private set; }
    public string BankReference { get; private set; } = default!;
    public string InvoiceReference { get; private set; } = default!;
    public decimal SystemTotal { get; private set; }
    public Guid ValidatedByUserId { get; private set; }
    public DateTime ValidatedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaymentValidation() { } // For ORM

    public static PaymentValidation Create(
        Guid careRequestId,
        string bankReference,
        string invoiceReference,
        decimal systemTotal,
        Guid validatedByUserId,
        DateTime validatedAtUtc)
    {
        if (careRequestId == Guid.Empty)
            throw new ArgumentException("CareRequestId cannot be empty.", nameof(careRequestId));

        if (string.IsNullOrWhiteSpace(bankReference))
            throw new ArgumentException("BankReference cannot be empty.", nameof(bankReference));

        if (string.IsNullOrWhiteSpace(invoiceReference))
            throw new ArgumentException("InvoiceReference cannot be empty.", nameof(invoiceReference));

        if (systemTotal < 0)
            throw new ArgumentException("SystemTotal cannot be negative.", nameof(systemTotal));

        if (validatedByUserId == Guid.Empty)
            throw new ArgumentException("ValidatedByUserId cannot be empty.", nameof(validatedByUserId));

        return new PaymentValidation
        {
            Id = Guid.NewGuid(),
            CareRequestId = careRequestId,
            BankReference = bankReference.Trim(), // normalized so reuse detection isn't fooled by whitespace
            InvoiceReference = invoiceReference,
            SystemTotal = decimal.Round(systemTotal, 2, MidpointRounding.AwayFromZero),
            ValidatedByUserId = validatedByUserId,
            ValidatedAtUtc = validatedAtUtc,
            CreatedAtUtc = validatedAtUtc,
        };
    }
}

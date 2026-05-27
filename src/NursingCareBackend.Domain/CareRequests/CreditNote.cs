namespace NursingCareBackend.Domain.CareRequests;

/// <summary>
/// An in-books record of money reversed on an already-Paid care request — a refund returned to the
/// client, or a credit applied against the collected revenue. The money-flow standard reverses paid
/// revenue MANUALLY and the domain deliberately keeps <see cref="CareRequest.Void"/> blocked after
/// <see cref="CareRequestStatus.Paid"/> so collected revenue can never silently disappear. This
/// entity makes that manual reversal auditable (amount, reason, optional external reference, who,
/// when). It is a ledger record only — it does NOT change the care request's status.
///
/// Created exclusively through <see cref="CareRequest.IssueCreditNote"/> so the aggregate enforces
/// the money invariants (request must be Paid; credits can never exceed what was paid).
/// </summary>
public sealed class CreditNote
{
    public Guid Id { get; private set; }
    public Guid CareRequestId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = default!;
    public string? Reference { get; private set; }
    public Guid IssuedByUserId { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }

    private CreditNote() { } // For ORM

    internal static CreditNote Create(
        Guid careRequestId,
        decimal amount,
        string reason,
        string? reference,
        Guid issuedByUserId,
        DateTime issuedAtUtc)
    {
        if (careRequestId == Guid.Empty)
            throw new ArgumentException("CareRequestId cannot be empty.", nameof(careRequestId));

        if (amount <= 0m)
            throw new ArgumentException("Credit note amount must be positive.", nameof(amount));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Credit note reason cannot be empty.", nameof(reason));

        if (issuedByUserId == Guid.Empty)
            throw new ArgumentException("IssuedByUserId cannot be empty.", nameof(issuedByUserId));

        return new CreditNote
        {
            Id = Guid.NewGuid(),
            CareRequestId = careRequestId,
            Amount = amount,
            Reason = reason.Trim(),
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            IssuedByUserId = issuedByUserId,
            IssuedAtUtc = issuedAtUtc,
        };
    }
}

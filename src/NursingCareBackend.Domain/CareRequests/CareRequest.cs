namespace NursingCareBackend.Domain.CareRequests;

public sealed class CareRequest
{
    public Guid Id { get; private set; }
    public Guid ResidentId { get; private set; }
    public string Description { get; private set; } = default!;
    public CareRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CareRequest() { } // For ORM

    private CareRequest(Guid residentId, string description)
    {
        if (residentId == Guid.Empty)
            throw new ArgumentException("ResidentId cannot be empty.", nameof(residentId));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        Id = Guid.NewGuid();
        ResidentId = residentId;
        Description = description;
        Status = CareRequestStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static CareRequest Create(Guid residentId, string description)
    {
        return new CareRequest(residentId, description);
    }
}

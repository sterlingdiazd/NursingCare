namespace NursingCareBackend.Domain.CareRequests;

public sealed class CareRequest
{
    public Guid Id { get; private set; }
    public Guid ResidentId { get; private set; }
    public string Description { get; private set; } = default!;
    public CareRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

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
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static CareRequest Create(Guid residentId, string description)
    {
        return new CareRequest(residentId, description);
    }

    public void Approve(DateTime transitionedAtUtc)
    {
        EnsurePending(nameof(Approve));

        Status = CareRequestStatus.Approved;
        ApprovedAtUtc = transitionedAtUtc;
        UpdatedAtUtc = transitionedAtUtc;
    }

    public void Reject(DateTime transitionedAtUtc)
    {
        EnsurePending(nameof(Reject));

        Status = CareRequestStatus.Rejected;
        RejectedAtUtc = transitionedAtUtc;
        UpdatedAtUtc = transitionedAtUtc;
    }

    public void Complete(DateTime transitionedAtUtc)
    {
        if (Status != CareRequestStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Care request can only be completed from Approved status. Current status is {Status}.");
        }

        Status = CareRequestStatus.Completed;
        CompletedAtUtc = transitionedAtUtc;
        UpdatedAtUtc = transitionedAtUtc;
    }

    private void EnsurePending(string actionName)
    {
        if (Status != CareRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Care request can only be {actionName.ToLowerInvariant()}d from Pending status. Current status is {Status}.");
        }
    }
}

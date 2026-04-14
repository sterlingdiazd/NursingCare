namespace NursingCareBackend.Domain.Payroll;

public sealed class ShiftRecord
{
    public Guid Id { get; private set; }
    public Guid CareRequestId { get; private set; }
    public Guid? NurseUserId { get; private set; }
    public DateTime? ScheduledStartUtc { get; private set; }
    public DateTime? ScheduledEndUtc { get; private set; }
    public DateTime? ActualStartUtc { get; private set; }
    public DateTime? ActualEndUtc { get; private set; }
    public ShiftRecordStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ShiftRecord() { }

    public static ShiftRecord Create(
        Guid careRequestId,
        Guid? nurseUserId,
        DateTime? scheduledStartUtc,
        DateTime? scheduledEndUtc,
        DateTime createdAtUtc)
    {
        if (careRequestId == Guid.Empty)
        {
            throw new ArgumentException("Care request is required.", nameof(careRequestId));
        }

        return new ShiftRecord
        {
            Id = Guid.NewGuid(),
            CareRequestId = careRequestId,
            NurseUserId = nurseUserId,
            ScheduledStartUtc = scheduledStartUtc,
            ScheduledEndUtc = scheduledEndUtc,
            Status = ShiftRecordStatus.Planned,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };
    }

    public void Complete(DateTime? actualStartUtc, DateTime actualEndUtc)
    {
        ActualStartUtc = actualStartUtc;
        ActualEndUtc = actualEndUtc;
        Status = ShiftRecordStatus.Completed;
        UpdatedAtUtc = actualEndUtc;
    }

    public void Reassign(Guid? nurseUserId, DateTime updatedAtUtc)
    {
        NurseUserId = nurseUserId;
        Status = ShiftRecordStatus.Changed;
        UpdatedAtUtc = updatedAtUtc;
    }
}

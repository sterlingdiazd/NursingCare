namespace NursingCareBackend.Domain.Payroll;

public sealed class ShiftChange
{
    public Guid Id { get; private set; }
    public Guid ShiftRecordId { get; private set; }
    public Guid? PreviousNurseUserId { get; private set; }
    public Guid? NewNurseUserId { get; private set; }
    public string Reason { get; private set; } = default!;
    public DateTime EffectiveAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ShiftChange() { }

    public static ShiftChange Create(Guid shiftRecordId, Guid? previousNurseUserId, Guid? newNurseUserId, string reason, DateTime effectiveAtUtc, DateTime createdAtUtc)
    {
        if (shiftRecordId == Guid.Empty)
        {
            throw new ArgumentException("Shift record is required.", nameof(shiftRecordId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Shift change reason is required.", nameof(reason));
        }

        return new ShiftChange
        {
            Id = Guid.NewGuid(),
            ShiftRecordId = shiftRecordId,
            PreviousNurseUserId = previousNurseUserId,
            NewNurseUserId = newNurseUserId,
            Reason = reason.Trim(),
            EffectiveAtUtc = effectiveAtUtc,
            CreatedAtUtc = createdAtUtc,
        };
    }
}

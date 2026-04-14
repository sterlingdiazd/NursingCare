namespace NursingCareBackend.Domain.Payroll;

public sealed class CompensationAdjustment
{
    public Guid Id { get; private set; }
    public Guid ServiceExecutionId { get; private set; }
    public string Label { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CompensationAdjustment() { }

    private CompensationAdjustment(Guid serviceExecutionId, string label, decimal amount, string? notes, DateTime createdAtUtc)
    {
        if (serviceExecutionId == Guid.Empty)
        {
            throw new ArgumentException("Service execution is required.", nameof(serviceExecutionId));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Adjustment label is required.", nameof(label));
        }

        Id = Guid.NewGuid();
        ServiceExecutionId = serviceExecutionId;
        Label = label.Trim();
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public static CompensationAdjustment Create(Guid serviceExecutionId, string label, decimal amount, string? notes, DateTime createdAtUtc)
        => new(serviceExecutionId, label, amount, notes, createdAtUtc);
}

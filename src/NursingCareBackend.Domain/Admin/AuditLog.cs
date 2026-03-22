namespace NursingCareBackend.Domain.Admin;

public sealed class AuditLog
{
  public Guid Id { get; set; }
  public Guid? ActorUserId { get; set; }
  public string ActorRole { get; set; } = default!;
  public string Action { get; set; } = default!;
  public string EntityType { get; set; } = default!;
  public string EntityId { get; set; } = default!;
  public string? Notes { get; set; }
  public string? MetadataJson { get; set; }
  public DateTime CreatedAtUtc { get; set; }
}

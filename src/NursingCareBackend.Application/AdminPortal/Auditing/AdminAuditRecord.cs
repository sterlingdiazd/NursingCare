namespace NursingCareBackend.Application.AdminPortal.Auditing;

public sealed record AdminAuditRecord(
  Guid? ActorUserId,
  string ActorRole,
  string Action,
  string EntityType,
  string EntityId,
  string? Notes,
  string? MetadataJson);

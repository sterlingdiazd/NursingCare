namespace NursingCareBackend.Application.AdminPortal.Auditing;

public sealed record AuditLogDetail(
  Guid Id,
  Guid? ActorUserId,
  string? ActorName,
  string? ActorEmail,
  string ActorRole,
  string Action,
  string EntityType,
  string EntityId,
  string? Notes,
  string? MetadataJson,
  DateTime CreatedAtUtc);

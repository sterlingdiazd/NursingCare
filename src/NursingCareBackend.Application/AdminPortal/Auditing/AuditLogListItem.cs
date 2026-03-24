namespace NursingCareBackend.Application.AdminPortal.Auditing;

public sealed record AuditLogListItem(
  Guid Id,
  Guid? ActorUserId,
  string? ActorName,
  string ActorRole,
  string Action,
  string EntityType,
  string EntityId,
  string? Notes,
  DateTime CreatedAtUtc);

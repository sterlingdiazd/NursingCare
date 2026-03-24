namespace NursingCareBackend.Application.AdminPortal.Auditing;

public sealed record AuditLogSearchRequest(
  Guid? ActorUserId,
  string? Action,
  string? EntityType,
  string? EntityId,
  DateTime? FromDate,
  DateTime? ToDate,
  int PageNumber = 1,
  int PageSize = 50);

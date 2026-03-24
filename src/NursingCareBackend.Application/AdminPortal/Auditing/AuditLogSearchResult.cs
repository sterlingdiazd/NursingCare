namespace NursingCareBackend.Application.AdminPortal.Auditing;

public sealed record AuditLogSearchResult(
  IReadOnlyList<AuditLogListItem> Items,
  int TotalCount,
  int PageNumber,
  int PageSize);

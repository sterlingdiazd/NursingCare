namespace NursingCareBackend.Application.AdminPortal.Auditing;

public interface IAuditLogQueryService
{
  Task<AuditLogSearchResult> SearchAsync(AuditLogSearchRequest request, CancellationToken cancellationToken = default);
  Task<AuditLogDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

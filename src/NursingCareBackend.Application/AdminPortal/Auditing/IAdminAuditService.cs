namespace NursingCareBackend.Application.AdminPortal.Auditing;

public interface IAdminAuditService
{
  Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken = default);
}

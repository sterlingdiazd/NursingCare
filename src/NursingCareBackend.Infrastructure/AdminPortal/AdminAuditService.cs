using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Domain.Admin;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminAuditService : IAdminAuditService
{
  private readonly NursingCareDbContext _dbContext;

  public AdminAuditService(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken = default)
  {
    var auditLog = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = record.ActorUserId,
      ActorRole = record.ActorRole.Trim(),
      Action = record.Action.Trim(),
      EntityType = record.EntityType.Trim(),
      EntityId = record.EntityId.Trim(),
      Notes = string.IsNullOrWhiteSpace(record.Notes) ? null : record.Notes.Trim(),
      MetadataJson = string.IsNullOrWhiteSpace(record.MetadataJson) ? null : record.MetadataJson,
      CreatedAtUtc = DateTime.UtcNow
    };

    _dbContext.AuditLogs.Add(auditLog);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}

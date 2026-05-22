using NursingCareBackend.Application.Abstractions;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Domain.Admin;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminAuditService : IAdminAuditService
{
  private readonly NursingCareDbContext _dbContext;
  private readonly ICorrelationContext _correlation;

  public AdminAuditService(NursingCareDbContext dbContext, ICorrelationContext correlation)
  {
    _dbContext = dbContext;
    _correlation = correlation;
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
      CorrelationId = _correlation.CorrelationId,
      CreatedAtUtc = DateTime.UtcNow
    };

    _dbContext.AuditLogs.Add(auditLog);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}

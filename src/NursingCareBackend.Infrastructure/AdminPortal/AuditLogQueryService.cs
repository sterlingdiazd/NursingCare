using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
  private readonly NursingCareDbContext _dbContext;

  public AuditLogQueryService(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<AuditLogSearchResult> SearchAsync(
    AuditLogSearchRequest request,
    CancellationToken cancellationToken = default)
  {
    var query = _dbContext.AuditLogs.AsNoTracking();

    if (request.ActorUserId.HasValue)
    {
      query = query.Where(log => log.ActorUserId == request.ActorUserId.Value);
    }

    if (!string.IsNullOrWhiteSpace(request.Action))
    {
      var action = request.Action.Trim();
      query = query.Where(log => log.Action.Contains(action));
    }

    if (!string.IsNullOrWhiteSpace(request.EntityType))
    {
      var entityType = request.EntityType.Trim();
      query = query.Where(log => log.EntityType.Contains(entityType));
    }

    if (!string.IsNullOrWhiteSpace(request.EntityId))
    {
      var entityId = request.EntityId.Trim();
      query = query.Where(log => log.EntityId.Contains(entityId));
    }

    if (request.FromDate.HasValue)
    {
      query = query.Where(log => log.CreatedAtUtc >= request.FromDate.Value);
    }

    if (request.ToDate.HasValue)
    {
      var toDate = request.ToDate.Value.AddDays(1);
      query = query.Where(log => log.CreatedAtUtc < toDate);
    }

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
      .OrderByDescending(log => log.CreatedAtUtc)
      .Skip((request.PageNumber - 1) * request.PageSize)
      .Take(request.PageSize)
      .Select(log => new
      {
        log.Id,
        log.ActorUserId,
        log.ActorRole,
        log.Action,
        log.EntityType,
        log.EntityId,
        log.Notes,
        log.CreatedAtUtc
      })
      .ToListAsync(cancellationToken);

    var actorUserIds = items
      .Where(item => item.ActorUserId.HasValue)
      .Select(item => item.ActorUserId!.Value)
      .Distinct()
      .ToList();

    var actorNames = await _dbContext.Users
      .AsNoTracking()
      .Where(u => actorUserIds.Contains(u.Id))
      .Select(u => new { u.Id, Name = u.DisplayName ?? (u.Name + " " + u.LastName) })
      .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

    var listItems = items.Select(item => new AuditLogListItem(
      item.Id,
      item.ActorUserId,
      item.ActorUserId.HasValue && actorNames.TryGetValue(item.ActorUserId.Value, out var name) ? name : null,
      item.ActorRole,
      item.Action,
      item.EntityType,
      item.EntityId,
      item.Notes,
      item.CreatedAtUtc
    )).ToList();

    return new AuditLogSearchResult(listItems, totalCount, request.PageNumber, request.PageSize);
  }

  public async Task<AuditLogDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
  {
    var log = await _dbContext.AuditLogs
      .AsNoTracking()
      .Where(l => l.Id == id)
      .Select(l => new
      {
        l.Id,
        l.ActorUserId,
        l.ActorRole,
        l.Action,
        l.EntityType,
        l.EntityId,
        l.Notes,
        l.MetadataJson,
        l.CreatedAtUtc
      })
      .FirstOrDefaultAsync(cancellationToken);

    if (log is null)
    {
      return null;
    }

    string? actorName = null;
    string? actorEmail = null;

    if (log.ActorUserId.HasValue)
    {
      var user = await _dbContext.Users
        .AsNoTracking()
        .Where(u => u.Id == log.ActorUserId.Value)
        .Select(u => new { Name = u.DisplayName ?? (u.Name + " " + u.LastName), u.Email })
        .FirstOrDefaultAsync(cancellationToken);

      if (user is not null)
      {
        actorName = user.Name;
        actorEmail = user.Email;
      }
    }

    return new AuditLogDetail(
      log.Id,
      log.ActorUserId,
      actorName,
      actorEmail,
      log.ActorRole,
      log.Action,
      log.EntityType,
      log.EntityId,
      log.Notes,
      log.MetadataJson,
      log.CreatedAtUtc
    );
  }
}

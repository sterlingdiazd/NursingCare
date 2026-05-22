using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminNotificationService : IAdminNotificationService
{
  private readonly NursingCareDbContext _dbContext;

  public AdminNotificationService(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<AdminNotificationListPage> ListForAdminAsync(
    Guid adminUserId,
    AdminNotificationListFilter filter,
    CancellationToken cancellationToken = default)
  {
    EnsureValidAdminUserId(adminUserId);

    var query = _dbContext.AdminNotifications
      .AsNoTracking()
      .Where(item => item.RecipientUserId == adminUserId);

    query = filter.Status switch
    {
      AdminNotificationStatus.Active => query.Where(item => item.ArchivedAtUtc == null),
      AdminNotificationStatus.Unread => query.Where(item => item.ArchivedAtUtc == null && item.ReadAtUtc == null),
      AdminNotificationStatus.ActionRequired => query.Where(item => item.ArchivedAtUtc == null && item.RequiresAction),
      AdminNotificationStatus.Archived => query.Where(item => item.ArchivedAtUtc != null),
      AdminNotificationStatus.All => query,
      _ => query.Where(item => item.ArchivedAtUtc == null),
    };

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
      .OrderByDescending(item => item.CreatedAtUtc)
      .Skip((filter.Page - 1) * filter.PageSize)
      .Take(filter.PageSize)
      .Select(item => new AdminNotificationListItem(
        item.Id,
        item.Category,
        item.Severity,
        item.Title,
        item.Body,
        item.EntityType,
        item.EntityId,
        item.DeepLinkPath,
        item.Source,
        item.RequiresAction,
        item.IsDismissed,
        item.CreatedAtUtc,
        item.ReadAtUtc,
        item.ArchivedAtUtc,
        item.CreatedBySystem))
      .ToListAsync(cancellationToken);

    return new AdminNotificationListPage(items, totalCount, filter.Page, filter.PageSize);
  }

  public async Task<AdminNotificationSummary> GetSummaryAsync(
    Guid adminUserId,
    CancellationToken cancellationToken = default)
  {
    EnsureValidAdminUserId(adminUserId);

    var baseQuery = _dbContext.AdminNotifications
      .AsNoTracking()
      .Where(item => item.RecipientUserId == adminUserId && item.ArchivedAtUtc == null);

    var total = await baseQuery.CountAsync(cancellationToken);
    var unread = await baseQuery.CountAsync(item => item.ReadAtUtc == null, cancellationToken);
    var requiresAction = await baseQuery.CountAsync(
      item => item.RequiresAction && item.ReadAtUtc == null && !item.IsDismissed,
      cancellationToken);
    var highSeverityUnread = await baseQuery.CountAsync(
      item => item.ReadAtUtc == null && item.Severity == "High",
      cancellationToken);

    return new AdminNotificationSummary(total, unread, requiresAction, highSeverityUnread);
  }

  public async Task MarkAsReadAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(adminUserId, notificationId, cancellationToken);
    if (!notification.ReadAtUtc.HasValue)
    {
      notification.ReadAtUtc = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  public async Task MarkAsUnreadAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(adminUserId, notificationId, cancellationToken);
    if (notification.ReadAtUtc.HasValue)
    {
      notification.ReadAtUtc = null;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  public async Task ArchiveAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(adminUserId, notificationId, cancellationToken);
    if (!notification.ArchivedAtUtc.HasValue)
    {
      notification.ArchivedAtUtc = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  public async Task DismissAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(adminUserId, notificationId, cancellationToken);
    if (!notification.IsDismissed)
    {
      notification.IsDismissed = true;
      notification.ReadAtUtc ??= DateTime.UtcNow;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  private async Task<Domain.Admin.AdminNotification> GetOwnedNotificationAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken)
  {
    EnsureValidAdminUserId(adminUserId);
    if (notificationId == Guid.Empty)
    {
      throw new ArgumentException("Notification ID cannot be empty.", nameof(notificationId));
    }

    var notification = await _dbContext.AdminNotifications
      .FirstOrDefaultAsync(
        item => item.Id == notificationId && item.RecipientUserId == adminUserId,
        cancellationToken);

    if (notification is null)
    {
      throw new KeyNotFoundException($"Notification '{notificationId}' was not found.");
    }

    return notification;
  }

  private static void EnsureValidAdminUserId(Guid adminUserId)
  {
    if (adminUserId == Guid.Empty)
    {
      throw new UnauthorizedAccessException("A valid admin user identifier is required.");
    }
  }
}

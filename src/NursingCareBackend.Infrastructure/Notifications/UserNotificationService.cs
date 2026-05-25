using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Notifications;

public sealed class UserNotificationService : IUserNotificationService
{
  private readonly NursingCareDbContext _dbContext;

  public UserNotificationService(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<UserNotificationListPage> ListForUserAsync(
    Guid userId,
    UserNotificationListFilter filter,
    CancellationToken cancellationToken = default)
  {
    EnsureValidUserId(userId);

    var query = _dbContext.UserNotifications
      .AsNoTracking()
      .Where(item => item.RecipientUserId == userId);

    query = filter.Status switch
    {
      UserNotificationStatus.Active => query.Where(item => item.ArchivedAtUtc == null),
      UserNotificationStatus.Unread => query.Where(item => item.ArchivedAtUtc == null && item.ReadAtUtc == null),
      UserNotificationStatus.ActionRequired => query.Where(item => item.ArchivedAtUtc == null && item.RequiresAction),
      UserNotificationStatus.Archived => query.Where(item => item.ArchivedAtUtc != null),
      UserNotificationStatus.All => query,
      _ => query.Where(item => item.ArchivedAtUtc == null),
    };

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
      .OrderByDescending(item => item.CreatedAtUtc)
      .Skip((filter.Page - 1) * filter.PageSize)
      .Take(filter.PageSize)
      .Select(item => new UserNotificationListItem(
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

    return new UserNotificationListPage(items, totalCount, filter.Page, filter.PageSize);
  }

  public async Task<UserNotificationSummary> GetSummaryAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
  {
    EnsureValidUserId(userId);

    var baseQuery = _dbContext.UserNotifications
      .AsNoTracking()
      .Where(item => item.RecipientUserId == userId && item.ArchivedAtUtc == null);

    var total = await baseQuery.CountAsync(cancellationToken);
    var unread = await baseQuery.CountAsync(item => item.ReadAtUtc == null, cancellationToken);
    var requiresAction = await baseQuery.CountAsync(
      item => item.RequiresAction && item.ReadAtUtc == null && !item.IsDismissed,
      cancellationToken);
    var highSeverityUnread = await baseQuery.CountAsync(
      item => item.ReadAtUtc == null && item.Severity == "High",
      cancellationToken);

    return new UserNotificationSummary(total, unread, requiresAction, highSeverityUnread);
  }

  public async Task MarkAsReadAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(userId, notificationId, cancellationToken);
    if (!notification.ReadAtUtc.HasValue)
    {
      notification.ReadAtUtc = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  public async Task MarkAsUnreadAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(userId, notificationId, cancellationToken);
    if (notification.ReadAtUtc.HasValue)
    {
      notification.ReadAtUtc = null;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  public async Task ArchiveAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(userId, notificationId, cancellationToken);
    if (!notification.ArchivedAtUtc.HasValue)
    {
      notification.ArchivedAtUtc = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  public async Task DismissAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default)
  {
    var notification = await GetOwnedNotificationAsync(userId, notificationId, cancellationToken);
    if (!notification.IsDismissed)
    {
      notification.IsDismissed = true;
      notification.ReadAtUtc ??= DateTime.UtcNow;
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  private async Task<Domain.Notifications.UserNotification> GetOwnedNotificationAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken)
  {
    EnsureValidUserId(userId);
    if (notificationId == Guid.Empty)
    {
      throw new ArgumentException("Notification ID cannot be empty.", nameof(notificationId));
    }

    var notification = await _dbContext.UserNotifications
      .FirstOrDefaultAsync(
        item => item.Id == notificationId && item.RecipientUserId == userId,
        cancellationToken);

    if (notification is null)
    {
      throw new KeyNotFoundException($"Notification '{notificationId}' was not found.");
    }

    return notification;
  }

  private static void EnsureValidUserId(Guid userId)
  {
    if (userId == Guid.Empty)
    {
      throw new UnauthorizedAccessException("A valid user identifier is required.");
    }
  }
}

namespace NursingCareBackend.Application.AdminPortal.Notifications;

public interface IAdminNotificationService
{
  Task<IReadOnlyList<AdminNotificationListItem>> ListForAdminAsync(
    Guid adminUserId,
    bool includeArchived,
    bool unreadOnly,
    CancellationToken cancellationToken = default);

  Task<AdminNotificationSummary> GetSummaryAsync(
    Guid adminUserId,
    CancellationToken cancellationToken = default);

  Task MarkAsReadAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default);

  Task MarkAsUnreadAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default);

  Task ArchiveAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default);

  Task DismissAsync(
    Guid adminUserId,
    Guid notificationId,
    CancellationToken cancellationToken = default);
}

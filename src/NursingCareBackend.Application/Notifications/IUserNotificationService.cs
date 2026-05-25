namespace NursingCareBackend.Application.Notifications;

public interface IUserNotificationService
{
  Task<UserNotificationListPage> ListForUserAsync(
    Guid userId,
    UserNotificationListFilter filter,
    CancellationToken cancellationToken = default);

  Task<UserNotificationSummary> GetSummaryAsync(
    Guid userId,
    CancellationToken cancellationToken = default);

  Task MarkAsReadAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default);

  Task MarkAsUnreadAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default);

  Task ArchiveAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default);

  Task DismissAsync(
    Guid userId,
    Guid notificationId,
    CancellationToken cancellationToken = default);
}

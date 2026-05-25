namespace NursingCareBackend.Application.Notifications;

public interface IUserNotificationPublisher
{
  Task PublishToUserAsync(
    UserNotificationPublishRequest request,
    CancellationToken cancellationToken = default);
}

namespace NursingCareBackend.Application.AdminPortal.Notifications;

public interface IAdminNotificationPublisher
{
  Task PublishToAdminsAsync(
    AdminNotificationPublishRequest request,
    CancellationToken cancellationToken = default);
}

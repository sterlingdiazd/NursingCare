using NursingCareBackend.Application.AdminPortal.Notifications;

namespace NursingCareBackend.Application.Tests;

public sealed class FakeAdminNotificationPublisher : IAdminNotificationPublisher
{
  public Task PublishToAdminsAsync(
    AdminNotificationPublishRequest request,
    CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }
}
using NursingCareBackend.Application.AdminPortal.Notifications;

namespace NursingCareBackend.Application.Tests;

public sealed class FakeAdminNotificationPublisher : IAdminNotificationPublisher
{
  public List<AdminNotificationPublishRequest> PublishedRequests { get; } = [];

  public Task PublishToAdminsAsync(
    AdminNotificationPublishRequest request,
    CancellationToken cancellationToken = default)
  {
    PublishedRequests.Add(request);
    return Task.CompletedTask;
  }
}
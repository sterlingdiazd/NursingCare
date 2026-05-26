using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Communications;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Infrastructure.Notifications;

namespace NursingCareBackend.Application.Tests;

public sealed class DemoRedirectUserNotificationPublisherTests
{
    private static UserNotificationPublishRequest Request() => new(
        RecipientUserId: Guid.NewGuid(),
        Category: "nurse_payment_confirmed",
        Severity: "Medium",
        Title: "Pago confirmado",
        Body: "cuerpo",
        EntityType: "NursePeriodPayment",
        EntityId: Guid.NewGuid().ToString(),
        DeepLinkPath: "/nurse/payroll",
        Source: "Nómina",
        RequiresAction: false);

    private static DemoRedirectUserNotificationPublisher Sut(IUserNotificationPublisher inner, bool enabled) =>
        new(inner,
            Options.Create(new DemoCommunicationsOptions { Enabled = enabled, ContactEmail = "owner@x.com", ContactPhone = "+18090000000" }),
            NullLogger<DemoRedirectUserNotificationPublisher>.Instance);

    [Fact]
    public async Task Suppresses_User_Notification_When_Demo_Enabled()
    {
        var inner = new FakeInner();
        await Sut(inner, enabled: true).PublishToUserAsync(Request());
        inner.Count.Should().Be(0); // a demo must not reach a real nurse/client
    }

    [Fact]
    public async Task Passes_Through_When_Demo_Disabled()
    {
        var inner = new FakeInner();
        await Sut(inner, enabled: false).PublishToUserAsync(Request());
        inner.Count.Should().Be(1);
    }
}

file sealed class FakeInner : IUserNotificationPublisher
{
    public int Count { get; private set; }
    public Task PublishToUserAsync(UserNotificationPublishRequest request, CancellationToken cancellationToken = default)
    {
        Count++;
        return Task.CompletedTask;
    }
}

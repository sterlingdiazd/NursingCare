using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Communications;
using NursingCareBackend.Application.Notifications;

namespace NursingCareBackend.Infrastructure.Notifications;

/// <summary>
/// Decorates <see cref="IUserNotificationPublisher"/> so that, while the DEMO communications redirect
/// is enabled, notifications to END USERS (nurses/clients) are SUPPRESSED. A push / in-app message
/// must never reach a real nurse or client during a demo — the same guarantee
/// <see cref="DemoRedirectEmailService"/> gives email and the wa.me builder gives WhatsApp. Admin
/// notifications are NOT affected (they go to the owner, who is the demo contact). There is no demo
/// contact USER id to redirect to, so the safe, fail-closed behavior is to suppress. In production
/// (Enabled = false) every call passes through unchanged to the inner publisher.
/// </summary>
public sealed class DemoRedirectUserNotificationPublisher : IUserNotificationPublisher
{
    private readonly IUserNotificationPublisher _inner;
    private readonly DemoCommunicationsOptions _options;
    private readonly ILogger<DemoRedirectUserNotificationPublisher> _logger;

    public DemoRedirectUserNotificationPublisher(
        IUserNotificationPublisher inner,
        IOptions<DemoCommunicationsOptions> options,
        ILogger<DemoRedirectUserNotificationPublisher> logger)
    {
        _inner = inner;
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishToUserAsync(
        UserNotificationPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.Enabled)
        {
            _logger.LogInformation(
                "DEMO mode: suppressed user notification '{Category}' to {RecipientUserId} (would have reached a real user).",
                request.Category,
                request.RecipientUserId);
            return Task.CompletedTask;
        }

        return _inner.PublishToUserAsync(request, cancellationToken);
    }
}

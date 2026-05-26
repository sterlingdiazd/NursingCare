using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Communications;
using NursingCareBackend.Application.Email;

namespace NursingCareBackend.Infrastructure.Email;

/// <summary>
/// Decorates <see cref="IEmailService"/> so that, while the DEMO communications redirect is
/// enabled, EVERY outgoing email is sent to a single configured demo contact (the owner)
/// instead of the real recipient. This guarantees a demo never emails a real nurse or client.
///
/// This is intended to be the OUTERMOST decorator in the email pipeline:
/// DemoRedirectEmailService → ArchivingEmailService → AcsEmailService.
/// Redirecting here (before archiving) means the on-disk .eml history reflects exactly what was
/// actually sent — i.e. the redirected recipient and the "[DEMO → original]" subject.
///
/// When the redirect is disabled, or no contact email is configured, every call passes through
/// unchanged to the inner service (production behavior).
/// </summary>
public sealed class DemoRedirectEmailService : IEmailService
{
    private readonly IEmailService _inner;
    private readonly DemoCommunicationsOptions _options;

    public DemoRedirectEmailService(
        IEmailService inner,
        IOptions<DemoCommunicationsOptions> options)
    {
        _inner = inner;
        _options = options.Value;
    }

    public Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (IsSuppressed())
        {
            return Task.CompletedTask;
        }
        var (recipient, finalSubject) = Redirect(recipientEmail, subject);
        return _inner.SendAsync(recipient, finalSubject, htmlBody, cancellationToken);
    }

    public Task SendWithAttachmentsAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData> attachments,
        CancellationToken cancellationToken = default)
    {
        if (IsSuppressed())
        {
            return Task.CompletedTask;
        }
        var (recipient, finalSubject) = Redirect(recipientEmail, subject);
        return _inner.SendWithAttachmentsAsync(recipient, finalSubject, htmlBody, attachments, cancellationToken);
    }

    /// <summary>
    /// Fail-closed: when demo mode is ON but no demo contact email is configured, SUPPRESS the email
    /// entirely rather than falling through to the real recipient. A half-configured demo must never
    /// message a real nurse/client.
    /// </summary>
    private bool IsSuppressed() => _options.Enabled && string.IsNullOrWhiteSpace(_options.ContactEmail);

    /// <summary>
    /// When the redirect is active, returns the configured demo contact as the recipient and a
    /// subject tagged with the original recipient ("[DEMO → original] subject") so it stays clear
    /// who the message was meant for. Otherwise returns the inputs unchanged.
    /// </summary>
    private (string Recipient, string Subject) Redirect(string recipientEmail, string subject)
    {
        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.ContactEmail))
        {
            return (_options.ContactEmail, $"[DEMO → {recipientEmail}] {subject}");
        }

        return (recipientEmail, subject);
    }
}

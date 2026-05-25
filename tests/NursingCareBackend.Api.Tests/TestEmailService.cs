using NursingCareBackend.Application.Email;

namespace NursingCareBackend.Api.Tests;

public sealed class TestEmailService : IEmailService
{
    public List<(string RecipientEmail, string Subject, string HtmlBody)> SentEmails { get; } = [];

    public Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        SentEmails.Add((recipientEmail, subject, htmlBody));
        return Task.CompletedTask;
    }

    public Task SendWithAttachmentsAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData> attachments,
        CancellationToken cancellationToken = default)
    {
        SentEmails.Add((recipientEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}

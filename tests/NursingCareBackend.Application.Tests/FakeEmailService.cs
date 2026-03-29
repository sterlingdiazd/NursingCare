using NursingCareBackend.Application.Email;

namespace NursingCareBackend.Application.Tests;

public sealed class FakeEmailService : IEmailService
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
}

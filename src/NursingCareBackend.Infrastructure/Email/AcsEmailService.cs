using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Email;

namespace NursingCareBackend.Infrastructure.Email;

/// <summary>
/// Sends transactional emails through Azure Communication Services.
/// </summary>
public sealed class AcsEmailService : IEmailService
{
    private readonly Lazy<EmailClient?> _client;
    private readonly EmailOptions _options;
    private readonly ILogger<AcsEmailService> _logger;

    public AcsEmailService(
        IOptions<EmailOptions> options,
        ILogger<AcsEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<EmailClient?>(() =>
        {
            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            {
                return null;
            }

            return new EmailClient(_options.ConnectionString);
        });
    }

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _client.Value;
            if (client is null || string.IsNullOrWhiteSpace(_options.SenderAddress))
            {
                _logger.LogWarning(
                    "Skipping email delivery to {Recipient} because ACS email configuration is incomplete.",
                    recipientEmail);
                return;
            }

            var emailSendOperation = await client.SendAsync(
                Azure.WaitUntil.Started,
                senderAddress: _options.SenderAddress,
                recipientAddress: recipientEmail,
                subject: subject,
                htmlContent: htmlBody,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Email queued to {Recipient} (operationId={OperationId})",
                recipientEmail,
                emailSendOperation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to {Recipient} with subject '{Subject}'",
                recipientEmail,
                subject);

            // Don't leak email failures to the caller — the forgot-password
            // endpoint should always return 200 for security reasons.
        }
    }
}

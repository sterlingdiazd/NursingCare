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
            await SendCoreAsync(recipientEmail, subject, htmlBody, attachments: null, cancellationToken);
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

    public Task SendWithAttachmentsAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData> attachments,
        CancellationToken cancellationToken = default)
    {
        // Unlike SendAsync, exceptions here propagate to the caller so it can record the
        // delivery outcome (e.g. the payment-confirmation flow sets voucherEmailSent).
        return SendCoreAsync(recipientEmail, subject, htmlBody, attachments, cancellationToken);
    }

    private async Task SendCoreAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData>? attachments,
        CancellationToken cancellationToken)
    {
        var client = _client.Value;
        if (client is null || string.IsNullOrWhiteSpace(_options.SenderAddress))
        {
            _logger.LogWarning(
                "Skipping email delivery to {Recipient} because ACS email configuration is incomplete.",
                recipientEmail);

            // The transport is not configured. For the attachment path the caller treats a
            // thrown exception as a delivery failure; signal that explicitly here.
            if (attachments is { Count: > 0 })
            {
                throw new InvalidOperationException("La configuración de correo (ACS) está incompleta; no se pudo enviar el comprobante.");
            }

            return;
        }

        var message = new EmailMessage(
            senderAddress: _options.SenderAddress,
            recipientAddress: recipientEmail,
            content: new EmailContent(subject) { Html = htmlBody });

        if (attachments is { Count: > 0 })
        {
            foreach (var attachment in attachments)
            {
                message.Attachments.Add(new EmailAttachment(
                    attachment.FileName,
                    attachment.ContentType,
                    BinaryData.FromBytes(attachment.Content)));
            }
        }

        var emailSendOperation = await client.SendAsync(
            Azure.WaitUntil.Started,
            message,
            cancellationToken);

        _logger.LogInformation(
            "Email queued to {Recipient} (operationId={OperationId})",
            recipientEmail,
            emailSendOperation.Id);
    }
}

namespace NursingCareBackend.Application.Email;

/// <summary>
/// A binary file attached to an outgoing email.
/// </summary>
/// <param name="FileName">File name shown to the recipient (e.g. "comprobante.pdf").</param>
/// <param name="ContentType">MIME type (e.g. "application/pdf").</param>
/// <param name="Content">Raw file bytes.</param>
public sealed record EmailAttachmentData(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Abstraction for sending transactional emails.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a plain-text or HTML email to a single recipient.
    /// </summary>
    Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an HTML email to a single recipient with one or more file attachments.
    /// </summary>
    Task SendWithAttachmentsAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData> attachments,
        CancellationToken cancellationToken = default);
}

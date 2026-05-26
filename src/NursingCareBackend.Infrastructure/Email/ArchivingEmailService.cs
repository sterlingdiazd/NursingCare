using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Email;

namespace NursingCareBackend.Infrastructure.Email;

/// <summary>
/// Decorates <see cref="IEmailService"/> to keep an on-disk history of every outgoing
/// email. Each message is written as a standard .eml (MIME) file — openable in any mail
/// client — under a date-segmented folder tree ({root}/yyyy/MM/dd/). Archiving is
/// best-effort: a write failure is logged and never interrupts (or alters the result of)
/// the underlying send.
/// </summary>
public sealed class ArchivingEmailService : IEmailService
{
    private readonly IEmailService _inner;
    private readonly EmailArchiveOptions _options;
    private readonly EmailOptions _emailOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ArchivingEmailService> _logger;

    public ArchivingEmailService(
        IEmailService inner,
        IOptions<EmailArchiveOptions> options,
        IOptions<EmailOptions> emailOptions,
        IHostEnvironment environment,
        ILogger<ArchivingEmailService> logger)
    {
        _inner = inner;
        _options = options.Value;
        _emailOptions = emailOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        // Archive before delegating so the history captures every email the app dispatches,
        // even when transport is unconfigured (dev) or the send later fails.
        Archive(recipientEmail, subject, htmlBody, attachments: null);
        return _inner.SendAsync(recipientEmail, subject, htmlBody, cancellationToken);
    }

    public Task SendWithAttachmentsAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData> attachments,
        CancellationToken cancellationToken = default)
    {
        Archive(recipientEmail, subject, htmlBody, attachments);
        return _inner.SendWithAttachmentsAsync(recipientEmail, subject, htmlBody, attachments, cancellationToken);
    }

    private void Archive(
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData>? attachments)
    {
        if (!_options.Enabled) return;

        try
        {
            var sentAt = DateTimeOffset.Now;
            var root = string.IsNullOrWhiteSpace(_options.RootPath)
                ? Path.Combine(_environment.ContentRootPath, "logs", "email-archive")
                : _options.RootPath;

            var folder = Path.Combine(
                root,
                sentAt.ToString("yyyy", CultureInfo.InvariantCulture),
                sentAt.ToString("MM", CultureInfo.InvariantCulture),
                sentAt.ToString("dd", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);

            var fromName = string.IsNullOrWhiteSpace(_emailOptions.SenderDisplayName) ? "NursingCare" : _emailOptions.SenderDisplayName;
            var fromAddress = string.IsNullOrWhiteSpace(_emailOptions.SenderAddress) ? "noreply@localhost" : _emailOptions.SenderAddress;

            var eml = BuildEml(sentAt, fromName, fromAddress, recipientEmail, subject, htmlBody, attachments, _options.MaxAttachmentBytes);
            var path = Path.Combine(folder, BuildFileName(sentAt, recipientEmail, subject));
            File.WriteAllText(path, eml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Best-effort retention: drop day-folders older than the cutoff. Never throws.
            PruneExpiredArchives(root, sentAt);
        }
        catch (Exception ex)
        {
            // Never let archiving break (or change the outcome of) the actual send.
            _logger.LogWarning(ex, "Email archive write failed (non-fatal) for {Recipient}.", recipientEmail);
        }
    }

    // Best-effort retention sweep: delete archived {root}/yyyy/MM/dd day-folders whose date is
    // older than (now - RetentionDays). Pruning is opportunistic (runs on write) and must never
    // throw — any failure is logged and swallowed so it can't affect archiving or the send.
    private void PruneExpiredArchives(string root, DateTimeOffset now)
    {
        if (_options.RetentionDays <= 0) return;

        try
        {
            if (!Directory.Exists(root)) return;

            var cutoff = now.Date.AddDays(-_options.RetentionDays);

            foreach (var yearDir in Directory.EnumerateDirectories(root))
            {
                if (!int.TryParse(Path.GetFileName(yearDir), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
                    continue;

                foreach (var monthDir in Directory.EnumerateDirectories(yearDir))
                {
                    if (!int.TryParse(Path.GetFileName(monthDir), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
                        || month is < 1 or > 12)
                        continue;

                    foreach (var dayDir in Directory.EnumerateDirectories(monthDir))
                    {
                        if (!int.TryParse(Path.GetFileName(dayDir), NumberStyles.None, CultureInfo.InvariantCulture, out var day)
                            || day is < 1 or > 31)
                            continue;

                        DateTime folderDate;
                        try
                        {
                            folderDate = new DateTime(year, month, day);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            continue; // e.g. a 31 under a 30-day month — leave it alone
                        }

                        if (folderDate < cutoff)
                        {
                            Directory.Delete(dayDir, recursive: true);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email archive retention prune failed (non-fatal).");
        }
    }

    /// <summary>Builds a date-prefixed, filesystem-safe .eml file name: yyyyMMdd-HHmmss-fff__recipient__subject.eml</summary>
    public static string BuildFileName(DateTimeOffset sentAt, string recipientEmail, string subject)
    {
        var stamp = sentAt.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var who = Slug(recipientEmail, 50);
        var what = Slug(subject, 60);
        return $"{stamp}__{who}__{what}.eml";
    }

    /// <summary>Serializes a single email to a standard MIME (.eml) document.</summary>
    /// <param name="maxAttachmentBytes">
    /// Attachments larger than this are replaced by a short text placeholder part instead of
    /// their base64 bytes. Zero or negative disables the cap (embed everything verbatim).
    /// </param>
    public static string BuildEml(
        DateTimeOffset sentAt,
        string fromName,
        string fromAddress,
        string recipientEmail,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachmentData>? attachments,
        long maxAttachmentBytes = 0)
    {
        const string crlf = "\r\n";
        var sb = new StringBuilder();

        sb.Append("From: ").Append(EncodeDisplayName(fromName)).Append(" <").Append(HeaderSafe(fromAddress)).Append('>').Append(crlf);
        sb.Append("To: ").Append(HeaderSafe(recipientEmail)).Append(crlf);
        sb.Append("Subject: ").Append(EncodeHeaderText(subject)).Append(crlf);
        sb.Append("Date: ").Append(FormatRfc5322Date(sentAt)).Append(crlf);
        sb.Append("MIME-Version: 1.0").Append(crlf);
        sb.Append("X-Archived-By: NursingCare").Append(crlf);

        var hasAttachments = attachments is { Count: > 0 };
        if (!hasAttachments)
        {
            sb.Append("Content-Type: text/html; charset=utf-8").Append(crlf);
            sb.Append("Content-Transfer-Encoding: base64").Append(crlf);
            sb.Append(crlf);
            sb.Append(Base64(Encoding.UTF8.GetBytes(htmlBody ?? string.Empty))).Append(crlf);
            return sb.ToString();
        }

        var boundary = "=_NC_" + Guid.NewGuid().ToString("N");
        sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append('"').Append(crlf);
        sb.Append(crlf);

        // HTML body part
        sb.Append("--").Append(boundary).Append(crlf);
        sb.Append("Content-Type: text/html; charset=utf-8").Append(crlf);
        sb.Append("Content-Transfer-Encoding: base64").Append(crlf);
        sb.Append(crlf);
        sb.Append(Base64(Encoding.UTF8.GetBytes(htmlBody ?? string.Empty))).Append(crlf);

        // Attachment parts
        foreach (var attachment in attachments!)
        {
            var fileName = HeaderSafe(string.IsNullOrWhiteSpace(attachment.FileName) ? "adjunto" : attachment.FileName);
            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType;
            var content = attachment.Content ?? [];

            sb.Append("--").Append(boundary).Append(crlf);

            // Size guard: oversized attachments are NOT embedded; we write a small text
            // placeholder part instead so the archive stays bounded but the attachment's
            // presence (name, type, size) is still recorded.
            if (maxAttachmentBytes > 0 && content.LongLength > maxAttachmentBytes)
            {
                var placeholder =
                    $"[Adjunto omitido del archivo: \"{fileName}\" ({contentType}, {content.LongLength} bytes) " +
                    $"excede el límite de {maxAttachmentBytes} bytes. El correo enviado sí incluyó el adjunto completo.]";
                sb.Append("Content-Type: text/plain; charset=utf-8").Append(crlf);
                sb.Append("Content-Transfer-Encoding: base64").Append(crlf);
                sb.Append("Content-Disposition: inline").Append(crlf);
                sb.Append(crlf);
                sb.Append(Base64(Encoding.UTF8.GetBytes(placeholder))).Append(crlf);
                continue;
            }

            sb.Append("Content-Type: ").Append(HeaderSafe(contentType)).Append("; name=\"").Append(fileName).Append('"').Append(crlf);
            sb.Append("Content-Transfer-Encoding: base64").Append(crlf);
            sb.Append("Content-Disposition: attachment; filename=\"").Append(fileName).Append('"').Append(crlf);
            sb.Append(crlf);
            sb.Append(Base64(content)).Append(crlf);
        }

        sb.Append("--").Append(boundary).Append("--").Append(crlf);
        return sb.ToString();
    }

    private static string Base64(byte[] bytes) =>
        Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);

    // Strip CR/LF to prevent header injection.
    private static string HeaderSafe(string value) =>
        (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

    // RFC 2047 encoded-word for headers with non-ASCII; plain (header-safe) otherwise.
    private static string EncodeHeaderText(string value)
    {
        var safe = HeaderSafe(value);
        if (IsAscii(safe)) return safe;
        return "=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(safe)) + "?=";
    }

    private static string EncodeDisplayName(string value)
    {
        var safe = HeaderSafe(value);
        if (IsAscii(safe)) return safe;
        return EncodeHeaderText(safe);
    }

    private static bool IsAscii(string value)
    {
        foreach (var c in value)
        {
            if (c > 127) return false;
        }
        return true;
    }

    private static string FormatRfc5322Date(DateTimeOffset value)
    {
        var main = value.ToString("ddd, dd MMM yyyy HH:mm:ss ", CultureInfo.InvariantCulture);
        var zone = value.ToString("zzz", CultureInfo.InvariantCulture).Replace(":", string.Empty); // +02:00 -> +0200
        return main + zone;
    }

    private static string Slug(string value, int maxLength)
    {
        var s = (value ?? string.Empty).Trim().ToLowerInvariant();
        s = RemoveDiacritics(s);
        s = Regex.Replace(s, "[^a-z0-9@._-]+", "-").Trim('-');
        if (s.Length == 0) s = "sin-asunto";
        return s.Length > maxLength ? s[..maxLength].TrimEnd('-') : s;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

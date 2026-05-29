using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Infrastructure.Email;
using Xunit;

namespace NursingCareBackend.Application.Tests;

public class ArchivingEmailServiceTests
{
    private static readonly DateTimeOffset SentAt = new(2026, 5, 25, 12, 30, 5, 123, TimeSpan.FromHours(-4));

    // ---- BuildEml (pure MIME serialization) ----

    [Fact]
    public void BuildEml_Without_Attachments_Is_Single_Html_Part()
    {
        var eml = ArchivingEmailService.BuildEml(
            SentAt, "Sol y Luna", "noreply@sol.com", "ana@x.com", "Comprobante de pago", "<p>Hola Ana</p>", null);

        Assert.Contains("From: Sol y Luna <noreply@sol.com>", eml);
        Assert.Contains("To: ana@x.com", eml);
        Assert.Contains("MIME-Version: 1.0", eml);
        Assert.Contains("Content-Type: text/html; charset=utf-8", eml);
        Assert.Contains("Content-Transfer-Encoding: base64", eml);
        Assert.DoesNotContain("multipart/mixed", eml);
        // Body is base64 of the UTF-8 HTML.
        Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("<p>Hola Ana</p>")), eml);
    }

    [Fact]
    public void BuildEml_Encodes_NonAscii_Subject_As_Encoded_Word()
    {
        var eml = ArchivingEmailService.BuildEml(
            SentAt, "NursingCare", "n@x.com", "ana@x.com", "Período de Nómina", "<p>x</p>", null);

        Assert.Contains("Subject: =?utf-8?B?", eml); // accented subject → RFC 2047 encoded-word
    }

    [Fact]
    public void BuildEml_With_Attachments_Is_Multipart_With_Attachment_Base64()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var eml = ArchivingEmailService.BuildEml(
            SentAt, "NursingCare", "n@x.com", "ana@x.com", "Comprobante", "<p>x</p>",
            new[] { new EmailAttachmentData("comprobante.pdf", "application/pdf", pdf) });

        Assert.Contains("Content-Type: multipart/mixed; boundary=", eml);
        Assert.Contains("Content-Disposition: attachment; filename=\"comprobante.pdf\"", eml);
        Assert.Contains(Convert.ToBase64String(pdf), eml);
    }

    [Fact]
    public void BuildEml_Skips_Oversized_Attachment_With_Text_Placeholder()
    {
        var big = new byte[2048];
        for (var i = 0; i < big.Length; i++) big[i] = (byte)'A';

        var eml = ArchivingEmailService.BuildEml(
            SentAt, "NursingCare", "n@x.com", "ana@x.com", "Comprobante", "<p>x</p>",
            new[] { new EmailAttachmentData("grande.pdf", "application/pdf", big) },
            maxAttachmentBytes: 1024);

        // The raw base64 of the oversized payload must NOT be embedded.
        Assert.DoesNotContain(Convert.ToBase64String(big), eml);
        // A placeholder text part recording the omission is written instead.
        Assert.Contains("Content-Type: text/plain; charset=utf-8", eml);
        // The placeholder text is base64-encoded (with line breaks); decode the whole .eml's
        // base64 payloads back to text and assert the placeholder copy is present.
        var decoded = DecodeAllBase64Parts(eml);
        Assert.Contains("Adjunto omitido del archivo: \"grande.pdf\" (application/pdf, 2048 bytes)", decoded);
        Assert.Contains("excede el límite de 1024 bytes", decoded);
    }

    // Concatenates every base64-decoded text part of a .eml, so assertions can match the
    // human-readable copy regardless of MIME line-break wrapping.
    private static string DecodeAllBase64Parts(string eml)
    {
        var sb = new StringBuilder();
        var lines = eml.Replace("\r\n", "\n").Split('\n');
        var buffer = new StringBuilder();

        void Flush()
        {
            if (buffer.Length == 0) return;
            try { sb.Append(Encoding.UTF8.GetString(Convert.FromBase64String(buffer.ToString()))).Append('\n'); }
            catch (FormatException) { /* not a base64 block */ }
            buffer.Clear();
        }

        var inBody = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("--") || line.Contains(": "))
            {
                Flush();
                inBody = false;
                continue;
            }
            if (line.Length == 0) { inBody = true; continue; }
            if (inBody) buffer.Append(line);
        }
        Flush();
        return sb.ToString();
    }

    [Fact]
    public void BuildEml_Embeds_Attachment_Under_The_Size_Cap()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var eml = ArchivingEmailService.BuildEml(
            SentAt, "NursingCare", "n@x.com", "ana@x.com", "Comprobante", "<p>x</p>",
            new[] { new EmailAttachmentData("comprobante.pdf", "application/pdf", pdf) },
            maxAttachmentBytes: 1024);

        Assert.Contains("Content-Disposition: attachment; filename=\"comprobante.pdf\"", eml);
        Assert.Contains(Convert.ToBase64String(pdf), eml);
    }

    [Fact]
    public async Task SendAsync_Prunes_DayFolders_Older_Than_RetentionDays()
    {
        var root = Path.Combine(Path.GetTempPath(), "nc-email-archive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var inner = new FakeEmailService();
        var service = CreateService(inner, root, retentionDays: 30);

        try
        {
            // Seed an expired day-folder (well past 30 days ago) and a recent one.
            var expired = Path.Combine(root, "2000", "01", "01");
            var recent = Path.Combine(
                root,
                DateTimeOffset.Now.ToString("yyyy"),
                DateTimeOffset.Now.ToString("MM"),
                DateTimeOffset.Now.ToString("dd"));
            Directory.CreateDirectory(expired);
            Directory.CreateDirectory(recent);
            File.WriteAllText(Path.Combine(expired, "old.eml"), "old");

            await service.SendAsync("ana@x.com", "Hola", "<p>cuerpo</p>");

            Assert.False(Directory.Exists(expired)); // pruned
            Assert.True(Directory.Exists(recent));    // kept
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildFileName_Is_Date_Prefixed_And_Slugged()
    {
        var name = ArchivingEmailService.BuildFileName(SentAt, "Ana@X.com", "Período de Nómina");

        Assert.StartsWith("20260525-123005-123__", name);
        Assert.EndsWith(".eml", name);
        Assert.Contains("ana@x.com", name);          // recipient lower-cased, kept
        Assert.Contains("periodo-de-nomina", name);  // subject slugged, diacritics stripped
    }

    // ---- Decorator behavior (writes archive + delegates to inner) ----

    [Fact]
    public async Task SendAsync_Writes_Eml_To_Date_Folder_And_Delegates()
    {
        var root = Path.Combine(Path.GetTempPath(), "nc-email-archive-test-" + Guid.NewGuid().ToString("N"));
        var inner = new FakeEmailService();
        var service = CreateService(inner, root);

        try
        {
            await service.SendAsync("ana@x.com", "Hola", "<p>cuerpo</p>");

            var files = Directory.GetFiles(root, "*.eml", SearchOption.AllDirectories);
            Assert.Single(files);
            // Segmented by date: .../yyyy/MM/dd/<file>.eml
            Assert.Matches(@"[/\\]\d{4}[/\\]\d{2}[/\\]\d{2}[/\\][^/\\]+\.eml$", files[0]);
            Assert.Contains("To: ana@x.com", File.ReadAllText(files[0]));
            // The real send still happened.
            Assert.Single(inner.SentEmails);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archiving_Disabled_Writes_Nothing_But_Still_Sends()
    {
        var root = Path.Combine(Path.GetTempPath(), "nc-email-archive-test-" + Guid.NewGuid().ToString("N"));
        var inner = new FakeEmailService();
        var service = CreateService(inner, root, enabled: false);

        await service.SendAsync("ana@x.com", "Hola", "<p>cuerpo</p>");

        Assert.False(Directory.Exists(root));
        Assert.Single(inner.SentEmails);
    }

    [Fact]
    public async Task SendWithAttachmentsAsync_Writes_Multipart_Eml_And_Delegates()
    {
        var root = Path.Combine(Path.GetTempPath(), "nc-email-archive-test-" + Guid.NewGuid().ToString("N"));
        var inner = new FakeEmailService();
        var service = CreateService(inner, root);

        try
        {
            await service.SendWithAttachmentsAsync(
                "ana@x.com", "Comprobante", "<p>cuerpo</p>",
                new[] { new EmailAttachmentData("comprobante.pdf", "application/pdf", new byte[] { 0x25, 0x50 }) });

            var files = Directory.GetFiles(root, "*.eml", SearchOption.AllDirectories);
            Assert.Single(files);
            Assert.Contains("multipart/mixed", File.ReadAllText(files[0]));
            Assert.Single(inner.SentEmails);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_Write_Failure_Is_NonFatal_And_Still_Sends()
    {
        // Point the archive root at an existing FILE so Directory.CreateDirectory fails.
        var blocker = Path.Combine(Path.GetTempPath(), "nc-email-archive-blocker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "x");
        var inner = new FakeEmailService();
        var service = CreateService(inner, blocker);

        try
        {
            // Best-effort archiving must never surface its failure to the caller.
            var ex = await Record.ExceptionAsync(() => service.SendAsync("ana@x.com", "Hola", "<p>x</p>"));
            Assert.Null(ex);
            Assert.Single(inner.SentEmails);
        }
        finally
        {
            if (File.Exists(blocker)) File.Delete(blocker);
        }
    }

    private static ArchivingEmailService CreateService(IEmailService inner, string root, bool enabled = true, int retentionDays = 3650) =>
        new(
            inner,
            Options.Create(new EmailArchiveOptions { Enabled = enabled, RootPath = root, RetentionDays = retentionDays }),
            Options.Create(new EmailOptions { SenderAddress = "noreply@sol.com", SenderDisplayName = "Sol y Luna" }),
            new StubHostEnvironment(),
            NullLogger<ArchivingEmailService>.Instance);

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

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

    private static ArchivingEmailService CreateService(IEmailService inner, string root, bool enabled = true) =>
        new(
            inner,
            Options.Create(new EmailArchiveOptions { Enabled = enabled, RootPath = root }),
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

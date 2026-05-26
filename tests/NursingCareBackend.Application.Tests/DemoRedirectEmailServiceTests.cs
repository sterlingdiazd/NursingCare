using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Communications;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Infrastructure.Email;
using Xunit;

namespace NursingCareBackend.Application.Tests;

public class DemoRedirectEmailServiceTests
{
    private const string OwnerEmail = "owner@solyluna.com";
    private const string RealRecipient = "ana.nurse@example.com";

    [Fact]
    public async Task SendAsync_When_Enabled_Redirects_To_ContactEmail_And_Tags_Subject()
    {
        var inner = new FakeEmailService();
        var service = CreateService(inner, enabled: true, contactEmail: OwnerEmail);

        await service.SendAsync(RealRecipient, "Comprobante de pago", "<p>cuerpo</p>");

        var sent = Assert.Single(inner.SentEmails);
        Assert.Equal(OwnerEmail, sent.RecipientEmail);
        Assert.Equal($"[DEMO → {RealRecipient}] Comprobante de pago", sent.Subject);
        Assert.Equal("<p>cuerpo</p>", sent.HtmlBody);
    }

    [Fact]
    public async Task SendWithAttachmentsAsync_When_Enabled_Redirects_To_ContactEmail_And_Tags_Subject()
    {
        var inner = new FakeEmailService();
        var service = CreateService(inner, enabled: true, contactEmail: OwnerEmail);

        await service.SendWithAttachmentsAsync(
            RealRecipient,
            "Comprobante de pago",
            "<p>cuerpo</p>",
            new[] { new EmailAttachmentData("comprobante.pdf", "application/pdf", new byte[] { 0x25, 0x50 }) });

        var sent = Assert.Single(inner.SentEmails);
        Assert.Equal(OwnerEmail, sent.RecipientEmail);
        Assert.Equal($"[DEMO → {RealRecipient}] Comprobante de pago", sent.Subject);
    }

    [Fact]
    public async Task SendAsync_When_Disabled_Passes_Through_Unchanged()
    {
        var inner = new FakeEmailService();
        var service = CreateService(inner, enabled: false, contactEmail: OwnerEmail);

        await service.SendAsync(RealRecipient, "Comprobante de pago", "<p>cuerpo</p>");

        var sent = Assert.Single(inner.SentEmails);
        Assert.Equal(RealRecipient, sent.RecipientEmail);
        Assert.Equal("Comprobante de pago", sent.Subject);
    }

    [Fact]
    public async Task SendAsync_When_Enabled_But_ContactEmail_Empty_Passes_Through_Unchanged()
    {
        var inner = new FakeEmailService();
        var service = CreateService(inner, enabled: true, contactEmail: "");

        await service.SendAsync(RealRecipient, "Comprobante de pago", "<p>cuerpo</p>");

        var sent = Assert.Single(inner.SentEmails);
        Assert.Equal(RealRecipient, sent.RecipientEmail);
        Assert.Equal("Comprobante de pago", sent.Subject);
    }

    private static DemoRedirectEmailService CreateService(
        IEmailService inner,
        bool enabled,
        string contactEmail) =>
        new(
            inner,
            Options.Create(new DemoCommunicationsOptions
            {
                Enabled = enabled,
                ContactEmail = contactEmail,
            }));
}

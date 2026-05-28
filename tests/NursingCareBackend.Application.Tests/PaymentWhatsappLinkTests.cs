using NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;
using NursingCareBackend.Application.Communications;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Covers the wa.me WhatsApp channel of the DEMO communications redirect — the third outbound
/// channel (alongside email and push, which have their own tests). Asserts that when demo mode is
/// on the payment-confirmation wa.me link can never reach a real nurse: it is redirected to the
/// demo contact, or suppressed (fail-closed) when the demo is half-configured.
/// </summary>
public class PaymentWhatsappLinkTests
{
    private const string NursePhone = "8099892465";        // real DR nurse phone (10 digits)
    private const string NurseNormalized = "18099892465";  // +1 wa.me form
    private const string DemoPhone = "8295551234";          // owner/demo contact
    private const string DemoNormalized = "18295551234";
    private const string PeriodLabel = "1-15 may 2026";

    [Fact]
    public void When_Disabled_Targets_The_Real_Nurse_Phone()
    {
        var url = ConfirmNursePeriodPaymentHandler.BuildWhatsappUrl(
            new DemoCommunicationsOptions { Enabled = false, ContactPhone = DemoPhone },
            NursePhone, PeriodLabel, out var redirectedToDemo);

        Assert.False(redirectedToDemo);
        Assert.StartsWith($"https://wa.me/{NurseNormalized}?text=", url);
    }

    [Fact]
    public void When_Enabled_With_Demo_Phone_Redirects_To_The_Demo_Contact()
    {
        var url = ConfirmNursePeriodPaymentHandler.BuildWhatsappUrl(
            new DemoCommunicationsOptions { Enabled = true, ContactPhone = DemoPhone },
            NursePhone, PeriodLabel, out var redirectedToDemo);

        Assert.True(redirectedToDemo);
        Assert.StartsWith($"https://wa.me/{DemoNormalized}?text=", url);
        // The real nurse number must NOT appear in a demo link.
        Assert.DoesNotContain(NurseNormalized, url);
    }

    [Fact]
    public void When_Enabled_But_Demo_Phone_Empty_Suppresses_The_Link()
    {
        var url = ConfirmNursePeriodPaymentHandler.BuildWhatsappUrl(
            new DemoCommunicationsOptions { Enabled = true, ContactPhone = "" },
            NursePhone, PeriodLabel, out var redirectedToDemo);

        // Fail-closed: a half-configured demo (enabled, no demo phone) must NOT fall through to the
        // real nurse — the link is suppressed entirely.
        Assert.False(redirectedToDemo);
        Assert.Equal(string.Empty, url);
    }

    [Fact]
    public void When_Disabled_And_No_Usable_Nurse_Phone_Returns_Empty()
    {
        var url = ConfirmNursePeriodPaymentHandler.BuildWhatsappUrl(
            new DemoCommunicationsOptions { Enabled = false, ContactPhone = DemoPhone },
            nursePhone: null, PeriodLabel, out var redirectedToDemo);

        Assert.False(redirectedToDemo);
        Assert.Equal(string.Empty, url);
    }
}

using FluentAssertions;
using NursingCareBackend.Application.CareRequests.PaymentOcr;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Unit tests for the OCR provider-chain resolver: which providers run, in what
/// order, given the configured mode and which providers have credentials. This
/// is the logic that makes OCR.space a fallback for Azure.
/// </summary>
public sealed class PaymentOcrProviderChainTests
{
    private static HashSet<string> Configured(params string[] names) =>
        new(names, StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Disabled")]
    [InlineData("disabled")]
    public void Resolve_returns_empty_when_disabled(string? mode)
    {
        var chain = PaymentOcrProviderChain.Resolve(
            mode,
            Configured(PaymentOcrProviderChain.Azure, PaymentOcrProviderChain.OcrSpace));

        chain.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_auto_orders_azure_then_google_then_ocrspace_when_all_configured()
    {
        var chain = PaymentOcrProviderChain.Resolve(
            "Auto",
            Configured(
                PaymentOcrProviderChain.OcrSpace,
                PaymentOcrProviderChain.GoogleVision,
                PaymentOcrProviderChain.Azure));

        chain.Should().Equal(
            PaymentOcrProviderChain.Azure,
            PaymentOcrProviderChain.GoogleVision,
            PaymentOcrProviderChain.OcrSpace);
    }

    [Fact]
    public void Resolve_auto_skips_unconfigured_providers_but_keeps_order()
    {
        var chain = PaymentOcrProviderChain.Resolve(
            "Auto",
            Configured(PaymentOcrProviderChain.GoogleVision, PaymentOcrProviderChain.OcrSpace));

        chain.Should().Equal(PaymentOcrProviderChain.GoogleVision, PaymentOcrProviderChain.OcrSpace);
    }

    [Fact]
    public void Resolve_explicit_google_returns_google_only_when_configured()
    {
        var chain = PaymentOcrProviderChain.Resolve(
            "GoogleVision",
            Configured(PaymentOcrProviderChain.Azure, PaymentOcrProviderChain.GoogleVision));

        chain.Should().Equal(PaymentOcrProviderChain.GoogleVision);
    }

    [Fact]
    public void Resolve_auto_falls_back_to_ocrspace_only_when_azure_not_configured()
    {
        var chain = PaymentOcrProviderChain.Resolve(
            "Auto",
            Configured(PaymentOcrProviderChain.OcrSpace));

        chain.Should().Equal(PaymentOcrProviderChain.OcrSpace);
    }

    [Fact]
    public void Resolve_auto_returns_empty_when_nothing_configured()
    {
        var chain = PaymentOcrProviderChain.Resolve("Auto", Configured());

        chain.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_explicit_azure_ignores_fallback()
    {
        var chain = PaymentOcrProviderChain.Resolve(
            "AzureVision",
            Configured(PaymentOcrProviderChain.Azure, PaymentOcrProviderChain.OcrSpace));

        chain.Should().Equal(PaymentOcrProviderChain.Azure);
    }

    [Fact]
    public void Resolve_explicit_azure_returns_empty_when_azure_not_configured()
    {
        var chain = PaymentOcrProviderChain.Resolve(
            "AzureVision",
            Configured(PaymentOcrProviderChain.OcrSpace));

        chain.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_explicit_ocrspace_is_case_insensitive()
    {
        var chain = PaymentOcrProviderChain.Resolve(
            "ocrspace",
            Configured(PaymentOcrProviderChain.OcrSpace));

        chain.Should().Equal(PaymentOcrProviderChain.OcrSpace);
    }
}

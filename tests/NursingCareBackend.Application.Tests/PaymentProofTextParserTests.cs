using FluentAssertions;
using NursingCareBackend.Application.CareRequests.PaymentOcr;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Unit tests for the provider-agnostic OCR text parser. Pure logic, no I/O -
/// every OCR backend feeds raw text through this and must produce identical
/// fields, warnings, and a non-binding draft.
/// </summary>
public sealed class PaymentProofTextParserTests
{
    private const string FullReceipt =
        "Transferencia exitosa\nBanreservas\nMonto: RD$ 1,500.00\nReferencia: 0123456789\nFecha: 2026-05-20";

    [Fact]
    public void Build_extracts_all_fields_from_a_clean_receipt()
    {
        var result = PaymentProofTextParser.Build(FullReceipt, "AzureVision", 1500.00m, []);

        result.ExtractedAmount.Should().Be(1500.00m);
        result.ExtractedBankReference.Should().Be("0123456789");
        result.ExtractedPaymentDate.Should().Be(new DateOnly(2026, 5, 20));
        result.ExtractedBank.Should().Be("Banreservas");
        result.Provider.Should().Be("AzureVision");
        result.Confidence.Should().Be(1.0m);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Build_warns_when_amount_does_not_match_invoice()
    {
        var result = PaymentProofTextParser.Build(FullReceipt, "OcrSpace", 2000.00m, []);

        result.ExtractedAmount.Should().Be(1500.00m);
        result.Warnings.Should().Contain("El monto leído no coincide con el total facturado.");
    }

    [Fact]
    public void Build_degrades_gracefully_on_empty_text()
    {
        var result = PaymentProofTextParser.Build(string.Empty, "OcrSpace", 1500.00m, []);

        result.ExtractedAmount.Should().BeNull();
        result.ExtractedBankReference.Should().BeNull();
        result.Confidence.Should().Be(0m);
        result.Provider.Should().Be("OcrSpace");
        result.DraftSentence.Should().Contain("Requiere confirmación bancaria");
        result.Warnings.Should().Contain("La app no leyó texto suficiente del comprobante.");
    }

    [Fact]
    public void Build_preserves_and_dedupes_initial_warnings()
    {
        var result = PaymentProofTextParser.Build(
            string.Empty,
            "Disabled",
            1500.00m,
            ["Lectura automática no configurada. Completa los datos manualmente."]);

        result.Warnings.Should().Contain("Lectura automática no configurada. Completa los datos manualmente.");
        result.Warnings.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void BuildManualEntry_is_neutral_with_no_failure_warnings()
    {
        var result = PaymentProofTextParser.BuildManualEntry("GoogleVision");

        result.ExtractedAmount.Should().BeNull();
        result.Confidence.Should().Be(0m);
        result.Provider.Should().Be("GoogleVision");
        result.Warnings.Should().BeEmpty();
        result.DraftSentence.Should().Contain("Ingresa los datos del pago");
        result.DraftSentence.Should().NotContain("No pudimos");
        result.DraftSentence.Should().NotContain("no se pudo");
    }

    [Fact]
    public void Build_draft_sentence_confirms_match_when_amount_equals_invoice()
    {
        var result = PaymentProofTextParser.Build(FullReceipt, "AzureVision", 1500.00m, []);

        result.DraftSentence.Should().Contain("El monto leído coincide con la factura.");
    }
}

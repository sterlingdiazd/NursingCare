using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

/// <summary>Anti-fraud: PaymentProof stores the structured claim (reference/amount/date/bank) and
/// exposes AmountMatches so the admin can flag a claimed amount that does not match the invoice.</summary>
public class PaymentProofClaimTests
{
    private static PaymentProof CreateWithClaim(decimal? amount, string? reference = "TRF-1")
        => PaymentProof.Create(
            careRequestId: Guid.NewGuid(),
            content: new byte[] { 1, 2, 3 },
            contentType: "image/png",
            note: null,
            uploadedByUserId: Guid.NewGuid(),
            uploadedAtUtc: DateTime.UtcNow,
            claimedBankReference: reference,
            claimedAmount: amount,
            claimedPaymentDate: new DateOnly(2026, 5, 26),
            payingBank: "  Banreservas  ");

    [Fact]
    public void Create_StoresAndTrimsClaimFields()
    {
        var proof = PaymentProof.Create(
            Guid.NewGuid(), new byte[] { 1 }, "image/png", null, Guid.NewGuid(), DateTime.UtcNow,
            claimedBankReference: "  TRF-99  ", claimedAmount: 4200m,
            claimedPaymentDate: new DateOnly(2026, 5, 26), payingBank: "  Popular  ");

        Assert.Equal("TRF-99", proof.ClaimedBankReference);
        Assert.Equal(4200m, proof.ClaimedAmount);
        Assert.Equal(new DateOnly(2026, 5, 26), proof.ClaimedPaymentDate);
        Assert.Equal("Popular", proof.PayingBank);
    }

    [Fact]
    public void Create_StoresOcrDraftMetadata_ForAuditComparison()
    {
        var assessedAt = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);

        var proof = PaymentProof.Create(
            Guid.NewGuid(), new byte[] { 1 }, "image/png", null, Guid.NewGuid(), DateTime.UtcNow,
            ocrDraftSentence: "  Borrador OCR: la app leyó un pago. Requiere confirmación bancaria.  ",
            ocrExtractedBankReference: "  TRF-OCR  ",
            ocrExtractedAmount: 1375m,
            ocrExtractedPaymentDate: new DateOnly(2026, 5, 27),
            ocrExtractedBank: "  Banreservas  ",
            ocrConfidence: 0.75m,
            ocrWarningsJson: "[\"Revisar monto\"]",
            ocrProvider: "AzureVision",
            ocrAssessedAtUtc: assessedAt,
            ocrClientEdited: true);

        Assert.Equal("Borrador OCR: la app leyó un pago. Requiere confirmación bancaria.", proof.OcrDraftSentence);
        Assert.Equal("TRF-OCR", proof.OcrExtractedBankReference);
        Assert.Equal(1375m, proof.OcrExtractedAmount);
        Assert.Equal(new DateOnly(2026, 5, 27), proof.OcrExtractedPaymentDate);
        Assert.Equal("Banreservas", proof.OcrExtractedBank);
        Assert.Equal(0.75m, proof.OcrConfidence);
        Assert.Equal("[\"Revisar monto\"]", proof.OcrWarningsJson);
        Assert.Equal("AzureVision", proof.OcrProvider);
        Assert.Equal(assessedAt, proof.OcrAssessedAtUtc);
        Assert.True(proof.OcrClientEdited);
    }

    [Fact]
    public void Create_BlankClaimStrings_StoredAsNull()
    {
        var proof = PaymentProof.Create(
            Guid.NewGuid(), new byte[] { 1 }, "image/png", null, Guid.NewGuid(), DateTime.UtcNow,
            claimedBankReference: "   ", payingBank: "  ");

        Assert.Null(proof.ClaimedBankReference);
        Assert.Null(proof.PayingBank);
    }

    [Fact]
    public void AmountMatches_TrueOnlyWhenEqual()
    {
        Assert.True(CreateWithClaim(4200m).AmountMatches(4200m));
        Assert.False(CreateWithClaim(4000m).AmountMatches(4200m));
        Assert.False(CreateWithClaim(null).AmountMatches(4200m)); // not reported → not a match
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Create_NonPositiveClaimedAmount_Throws(double amount)
    {
        Assert.Throws<ArgumentException>(() => CreateWithClaim((decimal)amount));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_InvalidOcrConfidence_Throws(double confidence)
    {
        Assert.Throws<ArgumentException>(() => PaymentProof.Create(
            Guid.NewGuid(), new byte[] { 1 }, "image/png", null, Guid.NewGuid(), DateTime.UtcNow,
            ocrConfidence: (decimal)confidence));
    }

    [Fact]
    public void Create_WithoutClaim_StillValid_BackCompat()
    {
        var proof = PaymentProof.Create(
            Guid.NewGuid(), new byte[] { 1 }, "image/png", "nota", Guid.NewGuid(), DateTime.UtcNow);

        Assert.Null(proof.ClaimedBankReference);
        Assert.Null(proof.ClaimedAmount);
        Assert.Null(proof.ClaimedPaymentDate);
        Assert.Null(proof.PayingBank);
    }
}

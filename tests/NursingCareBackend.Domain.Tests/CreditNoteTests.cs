using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

/// <summary>
/// T1.4 — credit note / refund against a Paid request. The aggregate owns the money invariants:
/// only a Paid request can be credited, and the running total of credit notes can never exceed the
/// amount paid (Total). Issuing a credit note never changes the request's status (the Void-after-Paid
/// block stays intact — that is verified in <see cref="CareRequestLifecycleTests"/>).
/// </summary>
public class CreditNoteTests
{
    // Total of a request built via CreateForTest/CreateCompletedRequest is 4200 (Total = 4200m).
    private const decimal PaidTotal = 4200m;

    [Fact]
    public void IssueCreditNote_FromPaid_WithinCap_ReturnsCreditNote_WithExpectedFields()
    {
        var request = CreatePaidRequest();
        var issuedBy = Guid.NewGuid();
        var at = new DateTime(2026, 5, 26, 14, 0, 0, DateTimeKind.Utc);

        var note = request.IssueCreditNote(
            amount: 1000m, reason: "  Reembolso parcial  ", reference: "  TRF-REF-9  ",
            issuedByUserId: issuedBy, issuedAtUtc: at, alreadyCreditedTotal: 0m);

        Assert.NotEqual(Guid.Empty, note.Id);
        Assert.Equal(request.Id, note.CareRequestId);
        Assert.Equal(1000m, note.Amount);
        Assert.Equal("Reembolso parcial", note.Reason);  // trimmed
        Assert.Equal("TRF-REF-9", note.Reference);        // trimmed
        Assert.Equal(issuedBy, note.IssuedByUserId);
        Assert.Equal(at, note.IssuedAtUtc);
    }

    [Fact]
    public void IssueCreditNote_DoesNotChangeRequestStatus()
    {
        var request = CreatePaidRequest();

        request.IssueCreditNote(500m, "Ajuste", null, Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Equal(CareRequestStatus.Paid, request.Status);
        Assert.False(request.IsVoided);
    }

    [Fact]
    public void IssueCreditNote_BlankReference_StoredAsNull()
    {
        var request = CreatePaidRequest();

        var note = request.IssueCreditNote(500m, "Ajuste", "   ", Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Null(note.Reference);
    }

    [Fact]
    public void IssueCreditNote_FullAmount_AtCap_Succeeds()
    {
        var request = CreatePaidRequest();

        var note = request.IssueCreditNote(PaidTotal, "Reembolso total", null, Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Equal(PaidTotal, note.Amount);
    }

    [Fact]
    public void IssueCreditNote_CumulativeExactlyAtCap_Succeeds()
    {
        var request = CreatePaidRequest();

        // 4000 already credited + 200 new = 4200 = Total (boundary, allowed).
        var note = request.IssueCreditNote(200m, "Resto", null, Guid.NewGuid(), DateTime.UtcNow, alreadyCreditedTotal: 4000m);

        Assert.Equal(200m, note.Amount);
    }

    [Fact]
    public void IssueCreditNote_ExceedingPaid_Throws()
    {
        var request = CreatePaidRequest();

        var act = () => request.IssueCreditNote(
            PaidTotal + 0.01m, "Demasiado", null, Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void IssueCreditNote_CumulativeExceedingPaid_Throws()
    {
        var request = CreatePaidRequest();

        // 4200 already credited (the whole amount) + 0.01 more would exceed Total.
        var act = () => request.IssueCreditNote(
            0.01m, "Otro", null, Guid.NewGuid(), DateTime.UtcNow, alreadyCreditedTotal: PaidTotal);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void IssueCreditNote_ZeroOrNegativeAmount_Throws(double amount)
    {
        var request = CreatePaidRequest();

        var act = () => request.IssueCreditNote(
            (decimal)amount, "Motivo", null, Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void IssueCreditNote_NegativeAlreadyCredited_Throws()
    {
        var request = CreatePaidRequest();

        var act = () => request.IssueCreditNote(
            100m, "Motivo", null, Guid.NewGuid(), DateTime.UtcNow, alreadyCreditedTotal: -1m);

        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IssueCreditNote_EmptyReason_Throws(string reason)
    {
        var request = CreatePaidRequest();

        var act = () => request.IssueCreditNote(
            100m, reason, null, Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void IssueCreditNote_FromCompleted_Throws()
    {
        var request = CreateCompletedRequest();

        var act = () => request.IssueCreditNote(
            100m, "Motivo", null, Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void IssueCreditNote_FromInvoiced_Throws()
    {
        var request = CreateCompletedRequest();
        request.Invoice("FAC-001", DateTime.UtcNow);

        var act = () => request.IssueCreditNote(
            100m, "Motivo", null, Guid.NewGuid(), DateTime.UtcNow, 0m);

        Assert.Throws<InvalidOperationException>(act);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CareRequest CreatePaidRequest()
    {
        var request = CreateCompletedRequest();
        request.Invoice("FAC-2026-001", DateTime.UtcNow);
        request.Pay("TRF-2026-001", DateTime.UtcNow);
        return request;
    }

    private static CareRequest CreateCompletedRequest()
    {
        var nurseUserId = Guid.NewGuid();
        var request = CareRequest.Create(new CareRequestCreateParams
        {
            UserID = Guid.NewGuid(),
            Description = "Service to bill",
            CareRequestReason = null,
            CareRequestType = "domicilio_24h",
            UnitType = "dia_completo",
            SuggestedNurse = null,
            AssignedNurse = nurseUserId,
            Unit = 1,
            Price = 3500m,
            Total = PaidTotal,
            ClientBasePrice = null,
            DistanceFactor = "local",
            ComplexityLevel = "estandar",
            MedicalSuppliesCost = null,
            CareRequestDate = null,
            PricingCategoryCode = "domicilio",
            CategoryFactorSnapshot = 1.2m,
            DistanceFactorMultiplierSnapshot = 1.0m,
            ComplexityMultiplierSnapshot = 1.0m,
            VolumeDiscountPercentSnapshot = 0,
            LineBeforeVolumeDiscount = null,
            UnitPriceAfterVolumeDiscount = null,
            SubtotalBeforeSupplies = null,
            CreatedAtUtc = DateTime.UtcNow,
        });
        request.Approve(DateTime.UtcNow.AddDays(-2));
        request.Complete(DateTime.UtcNow.AddDays(-1), nurseUserId);
        return request;
    }
}

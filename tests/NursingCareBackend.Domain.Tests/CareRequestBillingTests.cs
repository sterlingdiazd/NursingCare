using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

public class CareRequestBillingTests
{
    private static readonly DateTime UtcNow = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

    // ── MarkAsInvoiced ────────────────────────────────────────────────────────

    [Fact]
    public void MarkAsInvoiced_FromCompleted_Should_Transition_To_Invoiced()
    {
        var careRequest = CreateCompleted();

        careRequest.MarkAsInvoiced("FAC-001", UtcNow);

        Assert.Equal(CareRequestStatus.Invoiced, careRequest.Status);
        Assert.Equal("FAC-001", careRequest.InvoiceNumber);
        Assert.Equal(UtcNow, careRequest.InvoicedAtUtc);
        Assert.Equal(UtcNow, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void MarkAsInvoiced_FromPending_Should_Throw()
    {
        var careRequest = CreatePending();

        var act = () => careRequest.MarkAsInvoiced("FAC-001", UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void MarkAsInvoiced_WithEmptyInvoiceNumber_Should_Throw()
    {
        var careRequest = CreateCompleted();

        var act = () => careRequest.MarkAsInvoiced("   ", UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ── RecordPayment ─────────────────────────────────────────────────────────

    [Fact]
    public void RecordPayment_FromInvoiced_Should_Transition_To_Paid()
    {
        var careRequest = CreateInvoiced();

        careRequest.RecordPayment("REF-999", UtcNow);

        Assert.Equal(CareRequestStatus.Paid, careRequest.Status);
        Assert.Equal("REF-999", careRequest.BankReference);
        Assert.Equal(UtcNow, careRequest.PaidAtUtc);
        Assert.Equal(UtcNow, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void RecordPayment_FromCompleted_Should_Throw()
    {
        var careRequest = CreateCompleted();

        var act = () => careRequest.RecordPayment("REF-999", UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void RecordPayment_WithEmptyBankReference_Should_Throw()
    {
        var careRequest = CreateInvoiced();

        var act = () => careRequest.RecordPayment(string.Empty, UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ── VoidRequest ───────────────────────────────────────────────────────────

    [Fact]
    public void VoidRequest_FromInvoiced_Should_Transition_To_Voided()
    {
        var careRequest = CreateInvoiced();

        careRequest.VoidRequest("Error de facturación", UtcNow);

        Assert.Equal(CareRequestStatus.Voided, careRequest.Status);
        Assert.Equal("Error de facturación", careRequest.VoidReason);
        Assert.Equal(UtcNow, careRequest.VoidedAtUtc);
        Assert.Equal(UtcNow, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void VoidRequest_FromPaid_Should_Transition_To_Voided()
    {
        var careRequest = CreatePaid();

        careRequest.VoidRequest("Pago duplicado", UtcNow);

        Assert.Equal(CareRequestStatus.Voided, careRequest.Status);
        Assert.Equal("Pago duplicado", careRequest.VoidReason);
    }

    [Fact]
    public void VoidRequest_FromCompleted_Should_Throw()
    {
        var careRequest = CreateCompleted();

        var act = () => careRequest.VoidRequest("Motivo", UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void VoidRequest_WithEmptyReason_Should_Throw()
    {
        var careRequest = CreateInvoiced();

        var act = () => careRequest.VoidRequest("  ", UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ── GenerateReceipt ───────────────────────────────────────────────────────

    [Fact]
    public void GenerateReceipt_FromPaid_Should_Return_ReceiptNumber()
    {
        var careRequest = CreatePaid();

        var receiptNumber = careRequest.GenerateReceipt(UtcNow);

        Assert.NotNull(receiptNumber);
        Assert.StartsWith("R-", receiptNumber);
        Assert.Equal(receiptNumber, careRequest.ReceiptNumber);
        Assert.Equal(UtcNow, careRequest.ReceiptGeneratedAtUtc);
        Assert.Equal(UtcNow, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void GenerateReceipt_FromInvoiced_Should_Throw()
    {
        var careRequest = CreateInvoiced();

        var act = () => careRequest.GenerateReceipt(UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CareRequest CreatePending()
    {
        return CareRequest.Create(new CareRequestCreateParams
        {
            UserID = Guid.NewGuid(),
            Description = "Test billing request",
            CareRequestReason = null,
            CareRequestType = "domicilio_24h",
            UnitType = "dia_completo",
            SuggestedNurse = null,
            AssignedNurse = null,
            Unit = 1,
            Price = 3500m,
            Total = 4200m,
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
            CreatedAtUtc = new DateTime(2026, 4, 19, 8, 0, 0, DateTimeKind.Utc),
        });
    }

    private static CareRequest CreateCompleted()
    {
        var nurseId = Guid.NewGuid();
        var careRequest = CareRequest.Create(new CareRequestCreateParams
        {
            UserID = Guid.NewGuid(),
            Description = "Test billing request",
            CareRequestReason = null,
            CareRequestType = "domicilio_24h",
            UnitType = "dia_completo",
            SuggestedNurse = null,
            AssignedNurse = nurseId,
            Unit = 1,
            Price = 3500m,
            Total = 4200m,
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
            CreatedAtUtc = new DateTime(2026, 4, 19, 8, 0, 0, DateTimeKind.Utc),
        });
        careRequest.Approve(new DateTime(2026, 4, 19, 8, 30, 0, DateTimeKind.Utc));
        careRequest.Complete(new DateTime(2026, 4, 19, 9, 0, 0, DateTimeKind.Utc), nurseId);
        return careRequest;
    }

    private static CareRequest CreateInvoiced()
    {
        var careRequest = CreateCompleted();
        careRequest.MarkAsInvoiced("FAC-TEST-001", new DateTime(2026, 4, 19, 9, 30, 0, DateTimeKind.Utc));
        return careRequest;
    }

    private static CareRequest CreatePaid()
    {
        var careRequest = CreateInvoiced();
        careRequest.RecordPayment("BANK-REF-001", new DateTime(2026, 4, 19, 9, 45, 0, DateTimeKind.Utc));
        return careRequest;
    }
}

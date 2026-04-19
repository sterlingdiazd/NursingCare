using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

public class CareRequestLifecycleTests
{
    // ---- Invoice transitions ----

    [Fact]
    public void Invoice_Should_Transition_Completed_Request_To_Invoiced()
    {
        var careRequest = CreateCompletedRequest();
        var invoiceDate = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

        careRequest.Invoice("FAC-2026-001", invoiceDate);

        Assert.Equal(CareRequestStatus.Invoiced, careRequest.Status);
        Assert.Equal("FAC-2026-001", careRequest.InvoiceNumber);
        Assert.Equal(invoiceDate, careRequest.InvoicedAtUtc);
        Assert.Equal(invoiceDate, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Invoice_From_Non_Completed_Should_Throw()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Not completed");

        var act = () => careRequest.Invoice("FAC-001", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Invoice_With_Empty_InvoiceNumber_Should_Throw()
    {
        var careRequest = CreateCompletedRequest();

        var act = () => careRequest.Invoice("", DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Invoice_With_Whitespace_InvoiceNumber_Should_Throw()
    {
        var careRequest = CreateCompletedRequest();

        var act = () => careRequest.Invoice("   ", DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ---- Pay transitions ----

    [Fact]
    public void Pay_Should_Transition_Invoiced_Request_To_Paid()
    {
        var careRequest = CreateCompletedRequest();
        careRequest.Invoice("FAC-2026-001", DateTime.UtcNow);
        var paymentDate = new DateTime(2026, 4, 19, 11, 0, 0, DateTimeKind.Utc);

        careRequest.Pay("TRF-2026-001", paymentDate);

        Assert.Equal(CareRequestStatus.Paid, careRequest.Status);
        Assert.Equal(paymentDate, careRequest.PaidAtUtc);
        Assert.Equal(paymentDate, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Pay_From_Non_Invoiced_Should_Throw()
    {
        var careRequest = CreateCompletedRequest();

        var act = () => careRequest.Pay("TRF-001", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Pay_From_Pending_Should_Throw()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Pending");

        var act = () => careRequest.Pay("TRF-001", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Pay_With_Empty_BankReference_Should_Throw()
    {
        var careRequest = CreateCompletedRequest();
        careRequest.Invoice("FAC-001", DateTime.UtcNow);

        var act = () => careRequest.Pay("", DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ---- Void transitions ----

    [Fact]
    public void Void_Should_Transition_Completed_Request_To_Voided()
    {
        var careRequest = CreateCompletedRequest();
        var voidedAt = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

        careRequest.Void("Duplicate service entry", voidedAt);

        Assert.Equal(CareRequestStatus.Voided, careRequest.Status);
        Assert.Equal("Duplicate service entry", careRequest.VoidReason);
        Assert.Equal(voidedAt, careRequest.VoidedAtUtc);
        Assert.True(careRequest.IsVoided);
        Assert.Equal(voidedAt, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Void_Should_Transition_Invoiced_Request_To_Voided()
    {
        var careRequest = CreateCompletedRequest();
        careRequest.Invoice("FAC-001", DateTime.UtcNow);

        careRequest.Void("Error in client data", DateTime.UtcNow);

        Assert.Equal(CareRequestStatus.Voided, careRequest.Status);
        Assert.True(careRequest.IsVoided);
    }

    [Fact]
    public void Void_From_Pending_Should_Throw()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Pending");

        var act = () => careRequest.Void("reason", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Void_From_Paid_Should_Throw()
    {
        var careRequest = CreateCompletedRequest();
        careRequest.Invoice("FAC-001", DateTime.UtcNow);
        careRequest.Pay("TRF-001", DateTime.UtcNow);

        var act = () => careRequest.Void("Too late", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Void_With_Empty_VoidReason_Should_Throw()
    {
        var careRequest = CreateCompletedRequest();

        var act = () => careRequest.Void("", DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ---- IsVoided computed property ----

    [Fact]
    public void IsVoided_Returns_False_When_Not_Voided()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Active");

        Assert.False(careRequest.IsVoided);
    }

    // ---- Full lifecycle happy path ----

    [Fact]
    public void Full_Lifecycle_Completed_To_Invoiced_To_Paid_Should_Succeed()
    {
        var careRequest = CreateCompletedRequest();

        careRequest.Invoice("FAC-2026-001", DateTime.UtcNow);
        Assert.Equal(CareRequestStatus.Invoiced, careRequest.Status);

        careRequest.Pay("TRF-2026-001", DateTime.UtcNow);
        Assert.Equal(CareRequestStatus.Paid, careRequest.Status);

        Assert.False(careRequest.IsVoided);
        Assert.NotNull(careRequest.InvoiceNumber);
        Assert.NotNull(careRequest.InvoicedAtUtc);
        Assert.NotNull(careRequest.PaidAtUtc);
    }

    // ---- PaymentValidation entity ----

    [Fact]
    public void PaymentValidation_Create_Should_Set_All_Properties()
    {
        var careRequestId = Guid.NewGuid();
        var validatedByUserId = Guid.NewGuid();
        var validatedAt = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

        var pv = PaymentValidation.Create(
            careRequestId: careRequestId,
            bankReference: "TRF-2026-001",
            invoiceReference: "FAC-2026-001",
            systemTotal: 4200m,
            validatedByUserId: validatedByUserId,
            validatedAtUtc: validatedAt);

        Assert.NotEqual(Guid.Empty, pv.Id);
        Assert.Equal(careRequestId, pv.CareRequestId);
        Assert.Equal("TRF-2026-001", pv.BankReference);
        Assert.Equal("FAC-2026-001", pv.InvoiceReference);
        Assert.Equal(4200m, pv.SystemTotal);
        Assert.Equal(validatedByUserId, pv.ValidatedByUserId);
        Assert.Equal(validatedAt, pv.ValidatedAtUtc);
    }

    [Fact]
    public void PaymentValidation_Create_With_Empty_BankReference_Should_Throw()
    {
        var act = () => PaymentValidation.Create(
            Guid.NewGuid(), "", "FAC-001", 1000m, Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void PaymentValidation_Create_With_Negative_SystemTotal_Should_Throw()
    {
        var act = () => PaymentValidation.Create(
            Guid.NewGuid(), "TRF-001", "FAC-001", -1m, Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ---- Receipt entity ----

    [Fact]
    public void Receipt_Create_Should_Set_All_Properties()
    {
        var careRequestId = Guid.NewGuid();
        var generatedByUserId = Guid.NewGuid();
        var generatedAt = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);
        var content = new byte[] { 1, 2, 3 };

        var receipt = Receipt.Create(
            careRequestId: careRequestId,
            receiptNumber: "REC-20260419-0001",
            receiptContent: content,
            generatedByUserId: generatedByUserId,
            generatedAtUtc: generatedAt);

        Assert.NotEqual(Guid.Empty, receipt.Id);
        Assert.Equal(careRequestId, receipt.CareRequestId);
        Assert.Equal("REC-20260419-0001", receipt.ReceiptNumber);
        Assert.Equal(content, receipt.ReceiptContent);
        Assert.Equal(generatedByUserId, receipt.GeneratedByUserId);
        Assert.Equal(generatedAt, receipt.GeneratedAtUtc);
    }

    [Fact]
    public void Receipt_Create_With_Empty_ReceiptNumber_Should_Throw()
    {
        var act = () => Receipt.Create(
            Guid.NewGuid(), "", new byte[] { 1 }, Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Receipt_Create_With_Empty_Content_Should_Throw()
    {
        var act = () => Receipt.Create(
            Guid.NewGuid(), "REC-001", Array.Empty<byte>(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<ArgumentException>(act);
    }

    // ---- Helper methods ----

    private static CareRequest CreateCompletedRequest()
    {
        var nurseUserId = Guid.NewGuid();
        var careRequest = CreateForTest(Guid.NewGuid(), "Service to bill", assignedNurse: nurseUserId);
        careRequest.Approve(DateTime.UtcNow.AddDays(-2));
        careRequest.Complete(DateTime.UtcNow.AddDays(-1), nurseUserId);
        return careRequest;
    }

    private static CareRequest CreateForTest(Guid userID, string description, Guid? assignedNurse = null)
    {
        return CareRequest.Create(new CareRequestCreateParams
        {
            UserID = userID,
            Description = description,
            CareRequestReason = null,
            CareRequestType = "domicilio_24h",
            UnitType = "dia_completo",
            SuggestedNurse = null,
            AssignedNurse = assignedNurse,
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
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}

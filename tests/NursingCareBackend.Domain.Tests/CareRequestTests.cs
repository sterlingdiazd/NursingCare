using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

public class CareRequestTests
{
    [Fact]
    public void Create_Should_Create_CareRequest_With_Pending_Status()
    {
        // Arrange
        var userID = Guid.NewGuid();
        var description = "Help with daily activities";

        // Act
        var careRequest = CreateForTest(userID, description);

        // Assert
        Assert.NotEqual(Guid.Empty, careRequest.Id);
        Assert.Equal(userID, careRequest.UserID);
        Assert.Equal(description, careRequest.Description);
        Assert.Equal(CareRequestStatus.Pending, careRequest.Status);
        Assert.Equal(careRequest.CreatedAtUtc, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Create_With_Empty_ResidentId_Should_Throw()
    {
        // Act
        var act = () => CreateForTest(Guid.Empty, "Valid description");

        // Assert
        Assert.ThrowsAny<Exception>(act);
    }

    [Fact]
    public void Create_With_Empty_Description_Should_Throw()
    {
        // Act
        var act = () => CreateForTest(Guid.NewGuid(), "");

        // Assert
        Assert.ThrowsAny<Exception>(act);
    }

    [Fact]
    public void Create_With_Null_Description_Should_Throw()
    {
        // Act
        var act = () => CreateForTest(Guid.NewGuid(), null!);

        // Assert
        Assert.ThrowsAny<Exception>(act);
    }

    [Fact]
    public void Approve_Should_Transition_Request_To_Approved()
    {
        var nurseUserId = Guid.NewGuid();
        var careRequest = CreateForTest(Guid.NewGuid(), "Needs approval", assignedNurse: nurseUserId);
        var approvedAtUtc = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);

        careRequest.Approve(approvedAtUtc);

        Assert.Equal(CareRequestStatus.Approved, careRequest.Status);
        Assert.Equal(approvedAtUtc, careRequest.ApprovedAtUtc);
        Assert.Equal(approvedAtUtc, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Reject_Should_Transition_Request_To_Rejected()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Needs rejection");
        var rejectedAtUtc = new DateTime(2026, 3, 18, 12, 5, 0, DateTimeKind.Utc);

        careRequest.Reject(rejectedAtUtc);

        Assert.Equal(CareRequestStatus.Rejected, careRequest.Status);
        Assert.Equal(rejectedAtUtc, careRequest.RejectedAtUtc);
        Assert.Equal(rejectedAtUtc, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Complete_Should_Transition_Approved_Request_To_Completed()
    {
        var nurseUserId = Guid.NewGuid();
        var careRequest = CreateForTest(Guid.NewGuid(), "Needs completion", assignedNurse: nurseUserId);
        var approvedAtUtc = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);
        var completedAtUtc = new DateTime(2026, 3, 18, 12, 15, 0, DateTimeKind.Utc);

        careRequest.Approve(approvedAtUtc);
        careRequest.Complete(completedAtUtc, nurseUserId);

        Assert.Equal(CareRequestStatus.Completed, careRequest.Status);
        Assert.Equal(completedAtUtc, careRequest.CompletedAtUtc);
        Assert.Equal(completedAtUtc, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Complete_Before_Approval_Should_Throw()
    {
        var nurseUserId = Guid.NewGuid();
        var careRequest = CreateForTest(Guid.NewGuid(), "Needs completion", assignedNurse: nurseUserId);

        var act = () => careRequest.Complete(DateTime.UtcNow, nurseUserId);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Reject_After_Approval_Should_Throw()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Already approved", assignedNurse: Guid.NewGuid());
        careRequest.Approve(DateTime.UtcNow);

        var act = () => careRequest.Reject(DateTime.UtcNow.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void RejectPayment_Returns_PaymentReported_To_Invoiced_And_Clears_Proof()
    {
        var nurse = Guid.NewGuid();
        var cr = CreateForTest(Guid.NewGuid(), "Para rechazo", assignedNurse: nurse);
        var t = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);
        cr.Approve(t);
        cr.Complete(t.AddMinutes(10), nurse);
        cr.Invoice("SOL-202603-0001", t.AddMinutes(11));
        cr.ReportPayment(Guid.NewGuid(), t.AddMinutes(20));

        cr.RejectPayment("Comprobante ilegible", t.AddMinutes(30));

        Assert.Equal(CareRequestStatus.Invoiced, cr.Status);
        Assert.Null(cr.PaymentProofId);
        Assert.Null(cr.PaymentReportedAtUtc);
        Assert.Equal("Comprobante ilegible", cr.PaymentRejectionReason);
    }

    [Fact]
    public void RejectPayment_From_Invoiced_Should_Throw()
    {
        var nurse = Guid.NewGuid();
        var cr = CreateForTest(Guid.NewGuid(), "No reportado", assignedNurse: nurse);
        var t = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);
        cr.Approve(t);
        cr.Complete(t.AddMinutes(1), nurse);
        cr.Invoice("SOL-202603-0002", t.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => cr.RejectPayment("x", t.AddMinutes(3)));
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

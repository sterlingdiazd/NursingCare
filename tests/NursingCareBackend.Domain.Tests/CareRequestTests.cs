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
        var careRequest = CreateForTest(Guid.NewGuid(), "Needs approval");
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
        var careRequest = CreateForTest(Guid.NewGuid(), "Needs completion");
        var approvedAtUtc = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);
        var completedAtUtc = new DateTime(2026, 3, 18, 12, 15, 0, DateTimeKind.Utc);

        careRequest.Approve(approvedAtUtc);
        careRequest.Complete(completedAtUtc);

        Assert.Equal(CareRequestStatus.Completed, careRequest.Status);
        Assert.Equal(completedAtUtc, careRequest.CompletedAtUtc);
        Assert.Equal(completedAtUtc, careRequest.UpdatedAtUtc);
    }

    [Fact]
    public void Complete_Before_Approval_Should_Throw()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Needs completion");

        var act = () => careRequest.Complete(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Reject_After_Approval_Should_Throw()
    {
        var careRequest = CreateForTest(Guid.NewGuid(), "Already approved");
        careRequest.Approve(DateTime.UtcNow);

        var act = () => careRequest.Reject(DateTime.UtcNow.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(act);
    }

    private static CareRequest CreateForTest(Guid userID, string description)
    {
        return CareRequest.Create(
            userID: userID,
            description: description,
            careRequestReason: null,
            careRequestType: "domicilio_24h",
            nurseId: null,
            suggestedNurse: null,
            assignedNurse: null,
            unit: 1,
            price: null,
            clientBasePrice: null,
            distanceFactor: null,
            complexityLevel: null,
            medicalSuppliesCost: null,
            careRequestDate: null,
            existingSameUnitTypeCount: 0);
    }
}

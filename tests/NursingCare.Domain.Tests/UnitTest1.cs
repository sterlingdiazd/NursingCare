using NursingCare.Domain.CareRequests;
using Xunit;

namespace NursingCare.Domain.Tests;

public class CareRequestTests
{
    [Fact]
    public void Create_Should_Create_CareRequest_With_Pending_Status()
    {
        // Arrange
        var residentId = Guid.NewGuid();
        var description = "Help with daily activities";

        // Act
        var careRequest = CareRequest.Create(residentId, description);

        // Assert
        Assert.NotEqual(Guid.Empty, careRequest.Id);
        Assert.Equal(residentId, careRequest.ResidentId);
        Assert.Equal(description, careRequest.Description);
        Assert.Equal(CareRequestStatus.Pending, careRequest.Status);
    }
}

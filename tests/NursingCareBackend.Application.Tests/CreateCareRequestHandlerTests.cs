using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;
using Xunit;

namespace NursingCareBackend.Application.Tests;

public sealed class CreateCareRequestHandlerTests
{
  private static readonly string ConnectionString =
      Environment.GetEnvironmentVariable("NursingCare_TestSqlConnection")
      ?? throw new InvalidOperationException("Environment variable 'NursingCare_TestSqlConnection' must be set for application tests.");

  private static NursingCareDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<NursingCareDbContext>()
        .UseSqlServer(ConnectionString)
        .Options;

    var context = new NursingCareDbContext(options);
    context.Database.EnsureDeleted();
    context.Database.Migrate();

    return context;
  }

  [Fact]
  public async Task Handle_Should_Create_CareRequest_In_Database()
  {
    // Arrange
    await using var dbContext = CreateDbContext();
    var repository = new CareRequestRepository(dbContext);
    var handler = new CreateCareRequestHandler(repository);

    var residentId = Guid.NewGuid();
    var description = "Help with daily activities";

    var command = new CreateCareRequestCommand
    {
      ResidentId = residentId,
      Description = description
    };

    // Act
    var id = await handler.Handle(command, CancellationToken.None);

    // Assert
    var saved = await dbContext.CareRequests.FirstOrDefaultAsync(x => x.Id == id);

    Assert.NotNull(saved);
    Assert.Equal(residentId, saved!.ResidentId);
    Assert.Equal(description, saved.Description);
    Assert.Equal(CareRequestStatus.Pending, saved.Status);
  }
}

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;
using Xunit;

namespace NursingCareBackend.Application.Tests;

public sealed class CreateCareRequestHandlerTests
{
  private const string ConnectionString =
      "Server=localhost,1433;Database=NursingCareDb_Test;User Id=sa;Password=1202lingSter89*;TrustServerCertificate=True;";

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

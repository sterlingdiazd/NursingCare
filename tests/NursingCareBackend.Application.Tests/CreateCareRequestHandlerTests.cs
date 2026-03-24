using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Catalogs;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;
using Xunit;

namespace NursingCareBackend.Application.Tests;

public sealed class CreateCareRequestHandlerTests
{
  private static NursingCareDbContext CreateDbContext()
  {
    var connectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();
    var options = new DbContextOptionsBuilder<NursingCareDbContext>()
        .UseSqlServer(connectionString)
        .Options;

    var context = new NursingCareDbContext(options);
    context.Database.EnsureDeleted();
    // EnsureCreated uses the current EF model to create the database schema.
    // This avoids failures when migrations are applied to an unexpected pre-existing DB state.
    context.Database.EnsureCreated();
    CatalogSeeding.EnsureSeededAsync(context).GetAwaiter().GetResult();

    return context;
  }

  [Fact]
  public async Task Handle_Should_Create_CareRequest_In_Database()
  {
    // Arrange
    await using var dbContext = CreateDbContext();
    var repository = new CareRequestRepository(dbContext);
    var pricing = new CareRequestPricingCalculator(dbContext);
    var handler = new CreateCareRequestHandler(repository, pricing, new FakeAdminNotificationPublisher());

    var userID = Guid.NewGuid();
    var description = "Help with daily activities";

    var command = new CreateCareRequestCommand
    {
      UserID = userID,
      Description = description,
      CareRequestType = "domicilio_24h",
      Unit = 1
    };

    // Act
    var id = await handler.Handle(command, CancellationToken.None);

    // Assert
    var saved = await dbContext.CareRequests.FirstOrDefaultAsync(x => x.Id == id);

    Assert.NotNull(saved);
    Assert.Equal(userID, saved!.UserID);
    Assert.Equal(description, saved.Description);
    Assert.Equal(CareRequestStatus.Pending, saved.Status);
    Assert.Equal("domicilio_24h", saved.CareRequestType);
    Assert.Equal("dia_completo", saved.UnitType);
    Assert.Equal(1, saved.Unit);
    Assert.Null(saved.AssignedNurse);
  }
}

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Infrastructure.Catalogs;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;
using Xunit;

namespace NursingCareBackend.Application.Tests;

public sealed class CareRequestPricingCalculatorTests : IDisposable
{
  private readonly List<string> _createdConnectionStrings = new();

  [Fact]
  public async Task CalculateAsync_Should_Match_Legacy_Domicilio_24h_Defaults()
  {
    await using var db = await CreateSeededDbAsync();
    var calculator = new CareRequestPricingCalculator(db);

    var command = new CreateCareRequestCommand
    {
      UserID = Guid.NewGuid(),
      Description = "Test",
      CareRequestType = "domicilio_24h",
      Unit = 1,
      DistanceFactor = null,
      ComplexityLevel = null,
    };

    var result = await calculator.CalculateAsync(command, existingSameUnitTypeCount: 0, CancellationToken.None);

    Assert.Equal("domicilio", result.PricingCategoryCode);
    Assert.Equal(3500m, result.Price);
    Assert.Equal(4200m, result.Total);
    Assert.Equal(1.2m, result.CategoryFactorSnapshot);
    Assert.Equal(1.0m, result.DistanceFactorMultiplierSnapshot);
    Assert.Equal(1.0m, result.ComplexityMultiplierSnapshot);
    Assert.Equal(0, result.VolumeDiscountPercentSnapshot);
  }

  private async Task<NursingCareDbContext> CreateSeededDbAsync()
  {
    var connectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();
    _createdConnectionStrings.Add(connectionString);
    var options = new DbContextOptionsBuilder<NursingCareDbContext>()
        .UseSqlServer(connectionString)
        .Options;

    var context = new NursingCareDbContext(options);
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();
    await CatalogSeeding.EnsureSeededAsync(context);
    return context;
  }

  public void Dispose()
  {
    foreach (var connectionString in _createdConnectionStrings)
    {
      try
      {
        var options = new DbContextOptionsBuilder<NursingCareDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        using var db = new NursingCareDbContext(options);
        db.Database.EnsureDeleted();
      }
      catch { /* best-effort teardown; never fail the run */ }
    }
  }
}

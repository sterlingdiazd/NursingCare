using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;
using Xunit;

namespace NursingCareBackend.Api.Tests;

public sealed class CreateCareRequestApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public CreateCareRequestApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task POST_CareRequests_Should_Create_Record_In_Database()
  {
    // Arrange
    var client = _factory.CreateClient();

    var payload = new
    {
      residentId = Guid.NewGuid(),
      description = "Help with API integration test"
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/care-requests", payload);

    // Assert
    response.EnsureSuccessStatusCode();

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var exists = await db.CareRequests.AnyAsync();

    Assert.True(exists);
  }
}

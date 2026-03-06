using System.Net;
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
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateWriterToken(_factory.Services));

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

  [Fact]
  public async Task POST_CareRequests_Should_Return_BadRequest_When_Description_Is_Missing()
  {
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateWriterToken(_factory.Services));

    var payload = new
    {
      residentId = Guid.NewGuid(),
      // description omitted to trigger model validation error
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/care-requests", payload);

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
  }

  [Fact]
  public async Task POST_CareRequests_Should_Return_BadRequest_When_ResidentId_Is_Invalid_Guid()
  {
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateWriterToken(_factory.Services));

    var payload = new
    {
      residentId = "not-a-guid",
      description = "Invalid resident id"
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/care-requests", payload);

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
  }
}

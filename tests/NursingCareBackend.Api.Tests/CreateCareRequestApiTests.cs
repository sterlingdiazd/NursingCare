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
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

    var payload = new
    {
      careRequestDescription = "Help with API integration test",
      careRequestType = "domicilio_24h",
      unit = 1
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
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

    var payload = new
    {
      // careRequestDescription omitted to trigger model validation error
      careRequestType = "domicilio_24h",
      unit = 1
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/care-requests", payload);

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
  }

  [Fact]
  public async Task POST_CareRequests_Should_Ignore_Unexpected_UserId_Field()
  {
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

    var payload = new
    {
      userID = "not-a-guid",
      careRequestDescription = "Unexpected user id should be ignored",
      careRequestType = "domicilio_24h",
      unit = 1
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/care-requests", payload);

    // Assert
    response.EnsureSuccessStatusCode();
  }
}

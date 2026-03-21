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
    var (clientToken, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "create-db");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", clientToken);

    var payload = new
    {
      careRequestDescription = "Help with API integration test",
      careRequestType = "domicilio_24h",
      suggestedNurse = "Luisa",
      unit = 1
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/care-requests", payload);

    // Assert
    response.EnsureSuccessStatusCode();

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var created = await db.CareRequests
      .OrderByDescending(item => item.CreatedAtUtc)
      .FirstAsync(item => item.UserID == clientUserId && item.Description == payload.careRequestDescription);

    Assert.Equal(clientUserId, created.UserID);
    Assert.Equal("Luisa", created.SuggestedNurse);
    Assert.Null(created.AssignedNurse);
  }

  [Fact]
  public async Task POST_CareRequests_Should_Return_BadRequest_When_Description_Is_Missing()
  {
    // Arrange
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "create-validation");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", clientToken);

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
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "create-userid");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", clientToken);

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

  [Fact]
  public async Task POST_CareRequests_Should_Allow_Admin_To_Create_Request()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

    var response = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = "Admin-created request",
      careRequestType = "domicilio_24h",
      unit = 1
    });

    response.EnsureSuccessStatusCode();
  }

  [Fact]
  public async Task POST_CareRequests_Should_Return_Forbidden_For_Nurse()
  {
    var (nurseToken, _) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "create-forbidden-nurse");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", nurseToken);

    var response = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = "Nurse cannot create",
      careRequestType = "domicilio_24h",
      unit = 1
    });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }
}

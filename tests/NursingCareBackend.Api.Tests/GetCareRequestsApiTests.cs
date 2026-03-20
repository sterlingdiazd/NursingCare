using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace NursingCareBackend.Api.Tests;

public sealed class GetCareRequestsApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public GetCareRequestsApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GET_CareRequests_Should_Return_List()
  {
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateWriterToken(_factory.Services));

    var createPayload = new
    {
      careRequestDescription = "List endpoint test",
      careRequestType = "domicilio_24h",
      unit = 1
    };

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", createPayload);
    createResponse.EnsureSuccessStatusCode();

    // Act
    var listResponse = await client.GetAsync("/api/care-requests");

    // Assert
    listResponse.EnsureSuccessStatusCode();

    var items = await listResponse.Content.ReadFromJsonAsync<List<dynamic>>();

    Assert.NotNull(items);
    Assert.NotEmpty(items!);
  }

  [Fact]
  public async Task GET_CareRequests_ById_Should_Return_Item_When_Exists()
  {
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateWriterToken(_factory.Services));

    var createPayload = new
    {
      careRequestDescription = "Get by id endpoint test",
      careRequestType = "domicilio_24h",
      unit = 1
    };

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", createPayload);
    // TODO: Investigate the method EnsureSuccessStatusCode() and understand its purpose
    createResponse.EnsureSuccessStatusCode();

    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
    Assert.NotNull(created);

    // Act
    var getResponse = await client.GetAsync($"/api/care-requests/{created!.Id}");

    // Assert
    getResponse.EnsureSuccessStatusCode();

    var item = await getResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(item);
    Assert.Equal(created.Id, item!.Id);
    Assert.Equal(createPayload.careRequestDescription, item.CareRequestDescription);
  }

  [Fact]
  public async Task GET_CareRequests_ById_Should_Return_NotFound_When_Does_Not_Exist()
  {
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateWriterToken(_factory.Services));
    var id = Guid.NewGuid();

    // Act
    var response = await client.GetAsync($"/api/care-requests/{id}");

    // Assert
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  // TODO: DTOs should be created in a separate project and shared between the API and the tests
  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private sealed class CareRequestItem
  {
    public Guid Id { get; set; }
    public Guid UserID { get; set; }
    public string CareRequestDescription { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
  }
}

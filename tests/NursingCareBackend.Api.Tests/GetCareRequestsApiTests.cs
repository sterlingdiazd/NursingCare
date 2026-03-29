using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
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
  public async Task GET_CareRequests_Should_Return_All_Items_For_Admin()
  {
    var (firstClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "list-admin-1");
    var (secondClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "list-admin-2");
    await CreateCareRequestAsClientAsync(firstClientToken, "list-admin-1-request");
    await CreateCareRequestAsClientAsync(secondClientToken, "list-admin-2-request");

    var adminClient = _factory.CreateClient();
    adminClient.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

    var listResponse = await adminClient.GetAsync("/api/care-requests");

    listResponse.EnsureSuccessStatusCode();

    var items = await listResponse.Content.ReadFromJsonAsync<List<CareRequestItem>>();

    Assert.NotNull(items);
    Assert.Equal(2, items!.Count);
  }

  [Fact]
  public async Task GET_CareRequests_Should_Return_Only_Client_Own_Items()
  {
    var (clientToken, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "list-client-owner");
    await CreateCareRequestAsClientAsync(clientToken, "list-client-owner-request");
    var (otherClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "list-client-other");
    await CreateCareRequestAsClientAsync(otherClientToken, "list-client-other-request");

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var response = await client.GetAsync("/api/care-requests");

    response.EnsureSuccessStatusCode();
    var items = await response.Content.ReadFromJsonAsync<List<CareRequestItem>>();

    Assert.NotNull(items);
    Assert.Single(items!);
    Assert.Equal(clientUserId, items[0].UserID);
  }

  [Fact]
  public async Task GET_CareRequests_Should_Return_Only_Assigned_Items_For_Nurse()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "list-nurse-client");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "list-nurse-request");
    var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "list-nurse-owner");
    var (_, otherNurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "list-nurse-other");

    await AssignCareRequestAsync(createdId, nurseUserId);
    var otherRequestId = await CreateCareRequestAsClientAsync(clientToken, "list-nurse-other-request");
    await AssignCareRequestAsync(otherRequestId, otherNurseUserId);

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);

    var response = await client.GetAsync("/api/care-requests");

    response.EnsureSuccessStatusCode();
    var items = await response.Content.ReadFromJsonAsync<List<CareRequestItem>>();

    Assert.NotNull(items);
    Assert.Single(items!);
    Assert.Equal(createdId, items[0].Id);
    Assert.Equal(nurseUserId, Guid.Parse(items[0].AssignedNurse!));
  }

  [Fact]
  public async Task GET_CareRequests_ById_Should_Return_Item_When_Exists_For_Client_Owner()
  {
    var (clientToken, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "get-by-id");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "get-by-id-request");

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var getResponse = await client.GetAsync($"/api/care-requests/{createdId}");

    getResponse.EnsureSuccessStatusCode();

    var item = await getResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(item);
    Assert.Equal(createdId, item!.Id);
    Assert.Equal(clientUserId, item.UserID);
  }

  [Fact]
  public async Task GET_CareRequests_ById_Should_Return_NotFound_When_Does_Not_Exist()
  {
    // Arrange
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "get-not-found");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
    var id = Guid.NewGuid();

    // Act
    var response = await client.GetAsync($"/api/care-requests/{id}");

    // Assert
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task GET_CareRequests_Should_Return_Unauthorized_When_Non_Admin_Token_Misses_User_Id_Claim()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "missing-user-id");
    await CreateCareRequestAsClientAsync(clientToken, "missing-user-id-request");

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateTokenWithoutUserId(_factory.Services, "CLIENT"));

    var response = await client.GetAsync("/api/care-requests");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  private async Task<Guid> CreateCareRequestAsClientAsync(string clientToken, string description)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = description,
      careRequestType = "domicilio_24h",
      unit = 1
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
    Assert.NotNull(created);
    return created!.Id;
  }

  private async Task AssignCareRequestAsync(Guid careRequestId, Guid nurseUserId)
  {
    var adminClient = _factory.CreateClient();
    adminClient.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

    var response = await adminClient.PutAsJsonAsync($"/api/care-requests/{careRequestId}/assignment", new
    {
      assignedNurse = nurseUserId
    });

    response.EnsureSuccessStatusCode();
  }

  private sealed class CareRequestItem
  {
    public Guid Id { get; set; }
    public Guid UserID { get; set; }
    public string? AssignedNurse { get; set; }
    public string CareRequestDescription { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }
}

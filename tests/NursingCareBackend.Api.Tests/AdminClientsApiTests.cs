using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminClientsApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AdminClientsApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task POST_Create_Should_Create_Client_Profile_For_Admin_Only()
  {
    var adminClient = CreateAdminClient();
    var email = $"manual-client-{Guid.NewGuid():N}@nursingcare.local";

    var response = await adminClient.PostAsJsonAsync("/api/admin/clients", new
    {
      name = "Carla",
      lastName = "Jimenez",
      identificationNumber = "00122334456",
      phone = "8095550101",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<AdminClientDetailDto>();
    Assert.NotNull(payload);
    Assert.Equal(email, payload!.Email);
    Assert.True(payload.IsActive);
    Assert.True(payload.CanAdminCreateCareRequest);
    Assert.Equal(0, payload.OwnedCareRequestsCount);

    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"client-forbidden-{Guid.NewGuid():N}");
    var forbiddenClient = CreateAuthorizedClient(clientToken);
    var forbiddenResponse = await forbiddenClient.PostAsJsonAsync("/api/admin/clients", new
    {
      name = "Luisa",
      lastName = "Perez",
      identificationNumber = "00111222333",
      phone = "8095550111",
      email = $"forbidden-client-{Guid.NewGuid():N}@nursingcare.local",
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
  }

  [Fact]
  public async Task GET_List_Should_Filter_By_Status_And_Exclude_Admin_Only_Accounts()
  {
    var adminClient = CreateAdminClient();
    var active = await CreateAdminManagedClientAsync(adminClient, "client-list-active");
    var inactive = await CreateAdminManagedClientAsync(adminClient, "client-list-inactive");

    var deactivateResponse = await adminClient.PutAsJsonAsync(
      $"/api/admin/clients/{inactive.UserId}/active-state",
      new
      {
        isActive = false
      });

    deactivateResponse.EnsureSuccessStatusCode();

    var adminOnlyEmail = $"client-list-admin-only-{Guid.NewGuid():N}@nursingcare.local";
    var adminOnlyResponse = await adminClient.PostAsJsonAsync("/api/admin/admin-accounts", new
    {
      name = "Mariela",
      lastName = "Rojas",
      identificationNumber = "00199887766",
      phone = "8095550198",
      email = adminOnlyEmail,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    adminOnlyResponse.EnsureSuccessStatusCode();

    var activeResponse = await adminClient.GetAsync($"/api/admin/clients?status=active&search={Uri.EscapeDataString("client-list-active")}");
    activeResponse.EnsureSuccessStatusCode();
    var activePayload = await activeResponse.Content.ReadFromJsonAsync<AdminClientListItemDto[]>();

    Assert.NotNull(activePayload);
    Assert.Contains(activePayload!, item => item.UserId == active.UserId);
    Assert.DoesNotContain(activePayload!, item => item.UserId == inactive.UserId);

    var inactiveResponse = await adminClient.GetAsync($"/api/admin/clients?status=inactive&search={Uri.EscapeDataString("client-list-inactive")}");
    inactiveResponse.EnsureSuccessStatusCode();
    var inactivePayload = await inactiveResponse.Content.ReadFromJsonAsync<AdminClientListItemDto[]>();

    Assert.NotNull(inactivePayload);
    Assert.Contains(inactivePayload!, item => item.UserId == inactive.UserId && !item.IsActive);
    Assert.DoesNotContain(inactivePayload!, item => item.UserId == active.UserId);

    var adminOnlyListResponse = await adminClient.GetAsync($"/api/admin/clients?search={Uri.EscapeDataString(adminOnlyEmail)}");
    adminOnlyListResponse.EnsureSuccessStatusCode();
    var adminOnlyPayload = await adminOnlyListResponse.Content.ReadFromJsonAsync<AdminClientListItemDto[]>();

    Assert.NotNull(adminOnlyPayload);
    Assert.Empty(adminOnlyPayload!);
  }

  [Fact]
  public async Task GET_ById_Should_Return_CareRequest_History_For_Client()
  {
    var scenario = $"client-history-{Guid.NewGuid():N}";
    var (clientToken, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, scenario);
    var careRequestId = await CreateCareRequestAsClientAsync(clientToken, $"{scenario}-solicitud");
    var adminClient = CreateAdminClient();

    var response = await adminClient.GetAsync($"/api/admin/clients/{clientUserId}");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminClientDetailDto>();

    Assert.NotNull(payload);
    Assert.Equal(clientUserId, payload!.UserId);
    Assert.True(payload.CanAdminCreateCareRequest);
    Assert.True(payload.HasHistoricalCareRequests);
    Assert.True(payload.OwnedCareRequestsCount >= 1);
    Assert.Contains(payload.CareRequestHistory, item => item.CareRequestId == careRequestId && item.Status == "Pending");
  }

  [Fact]
  public async Task PUT_Update_Should_Edit_Client_Identity_Fields()
  {
    var adminClient = CreateAdminClient();
    var created = await CreateAdminManagedClientAsync(adminClient, "client-update");

    var response = await adminClient.PutAsJsonAsync(
      $"/api/admin/clients/{created.UserId}",
      new
      {
        name = "Mariela",
        lastName = "Santos",
        identificationNumber = "00111112222",
        phone = "8095550177",
        email = $"client-update-edited-{Guid.NewGuid():N}@nursingcare.local"
      });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminClientDetailDto>();

    Assert.NotNull(payload);
    Assert.Equal("Mariela", payload!.Name);
    Assert.Equal("Santos", payload.LastName);
    Assert.Equal("00111112222", payload.IdentificationNumber);
    Assert.Equal("8095550177", payload.Phone);
    Assert.Contains("client-update-edited-", payload.Email, StringComparison.Ordinal);
  }

  [Fact]
  public async Task PUT_ActiveState_Should_Deactivate_Client_And_Block_Admin_CareRequest_Creation()
  {
    var adminClient = CreateAdminClient();
    var created = await CreateAdminManagedClientAsync(adminClient, "client-deactivate");

    var response = await adminClient.PutAsJsonAsync(
      $"/api/admin/clients/{created.UserId}/active-state",
      new
      {
        isActive = false
      });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminClientDetailDto>();

    Assert.NotNull(payload);
    Assert.False(payload!.IsActive);
    Assert.False(payload.CanAdminCreateCareRequest);

    var createCareRequestResponse = await adminClient.PostAsJsonAsync("/api/admin/care-requests", new
    {
      clientUserId = created.UserId,
      careRequestDescription = "solicitud bloqueada por inactividad",
      careRequestType = "domicilio_24h",
      unit = 1
    });

    Assert.Equal(HttpStatusCode.BadRequest, createCareRequestResponse.StatusCode);
  }

  [Fact]
  public async Task POST_Create_Should_Return_BadRequest_For_Invalid_Field_Formats()
  {
    var adminClient = CreateAdminClient();

    var response = await adminClient.PostAsJsonAsync("/api/admin/clients", new
    {
      name = "Carla2",
      lastName = "Jimenez",
      identificationNumber = "0012233445",
      phone = "80955501AA",
      email = "correo-invalido",
      password = "123",
      confirmPassword = "1234"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
  }

  private HttpClient CreateAuthorizedClient(string token)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
  }

  private async Task<AdminClientDetailDto> CreateAdminManagedClientAsync(HttpClient adminClient, string scenario)
  {
    var email = $"{scenario}-{Guid.NewGuid():N}@nursingcare.local";
    var response = await adminClient.PostAsJsonAsync("/api/admin/clients", new
    {
      name = "Carla",
      lastName = "Jimenez",
      identificationNumber = "00122334456",
      phone = "8095550101",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminClientDetailDto>();
    Assert.NotNull(payload);
    return payload!;
  }

  private async Task<Guid> CreateCareRequestAsClientAsync(
    string clientToken,
    string description)
  {
    var client = CreateAuthorizedClient(clientToken);

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = description,
      careRequestType = "domicilio_24h",
      unit = 1
    });

    createResponse.EnsureSuccessStatusCode();
    var payload = await createResponse.Content.ReadFromJsonAsync<CreateResponseDto>();
    Assert.NotNull(payload);
    return payload!.Id;
  }

  private sealed class CreateResponseDto
  {
    public Guid Id { get; set; }
  }

  private sealed class AdminClientListItemDto
  {
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
  }

  private sealed class AdminClientCareRequestHistoryItemDto
  {
    public Guid CareRequestId { get; set; }
    public string Status { get; set; } = string.Empty;
  }

  private sealed class AdminClientDetailDto
  {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? IdentificationNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public int OwnedCareRequestsCount { get; set; }
    public bool HasHistoricalCareRequests { get; set; }
    public bool CanAdminCreateCareRequest { get; set; }
    public List<AdminClientCareRequestHistoryItemDto> CareRequestHistory { get; set; } = [];
  }
}

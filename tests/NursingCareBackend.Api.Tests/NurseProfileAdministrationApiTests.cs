using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NursingCareBackend.Api.Tests;

public sealed class NurseProfileAdministrationApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public NurseProfileAdministrationApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GET_Pending_Should_Return_Nurses_Still_Under_Admin_Completion()
  {
    var nurseToken = await RegisterAndLoginPendingNurseAsync("pending-list");
    var adminClient = CreateAdminClient();

    var response = await adminClient.GetAsync("/api/admin/nurse-profiles/pending");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<PendingNurseProfileDto[]>();
    Assert.NotNull(payload);
    Assert.NotEmpty(payload!);
    Assert.Contains(payload, item => item.Email.Contains("pending-list-", StringComparison.Ordinal));

    var nurseClient = _factory.CreateClient();
    nurseClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var forbiddenResponse = await nurseClient.GetAsync("/api/admin/nurse-profiles/pending");
    Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
  }

  [Fact]
  public async Task GET_ById_Should_Return_Combined_User_And_Nurse_Profile_For_Admin()
  {
    var registration = await RegisterPendingNurseAsync("detail");
    var adminClient = CreateAdminClient();

    var response = await adminClient.GetAsync($"/api/admin/nurse-profiles/{registration.UserId}");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<NurseProfileAdminDto>();
    Assert.NotNull(payload);
    Assert.Equal(registration.UserId, payload!.UserId);
    Assert.Equal("Atencion domiciliaria", payload.Specialty);
    Assert.False(payload.NurseProfileIsActive);
  }

  [Fact]
  public async Task GET_Inactive_Should_Return_Completed_Nurses_With_Operational_Access_Disabled()
  {
    var adminClient = CreateAdminClient();
    var inactive = await CreateAdminNurseAsync(adminClient, "inactive-list", isOperationallyActive: false);
    var active = await CreateAdminNurseAsync(adminClient, "active-list", isOperationallyActive: true);

    var response = await adminClient.GetAsync("/api/admin/nurse-profiles/inactive");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<NurseProfileSummaryDto[]>();
    Assert.NotNull(payload);
    Assert.Contains(payload!, item => item.UserId == inactive.UserId);
    Assert.DoesNotContain(payload!, item => item.UserId == active.UserId);
  }

  [Fact]
  public async Task POST_Create_Should_Create_Nurse_Profile_For_Admin_Only()
  {
    var adminClient = CreateAdminClient();

    var response = await adminClient.PostAsJsonAsync("/api/admin/nurse-profiles", new
    {
      name = "Laura",
      lastName = "Gomez",
      identificationNumber = "00111111111",
      phone = "8095550199",
      email = $"created-{Guid.NewGuid():N}@nursingcare.local",
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Cuidados intensivos",
      licenseId = "55",
      bankName = "Banco Central",
      accountNumber = "123456",
      category = "Senior",
      isOperationallyActive = false
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var payload = await response.Content.ReadFromJsonAsync<NurseProfileAdminDto>();
    Assert.NotNull(payload);
    Assert.False(payload!.UserIsActive);
    Assert.False(payload.NurseProfileIsActive);
    Assert.False(payload.IsPendingReview);
    Assert.False(payload.HasHistoricalCareRequests);

    var nurseToken = await RegisterAndLoginPendingNurseAsync("create-forbidden");
    var nurseClient = CreateAuthorizedClient(nurseToken);

    var forbiddenResponse = await nurseClient.PostAsJsonAsync("/api/admin/nurse-profiles", new
    {
      name = "Laura",
      lastName = "Gomez",
      identificationNumber = "00111111111",
      phone = "8095550199",
      email = $"blocked-{Guid.NewGuid():N}@nursingcare.local",
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Cuidados intensivos",
      bankName = "Banco Central",
      category = "Senior",
      isOperationallyActive = true
    });

    Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
  }

  [Fact]
  public async Task PUT_Complete_Should_Finalize_Nurse_Profile_And_Enable_Operational_Access()
  {
    var registration = await RegisterPendingNurseAsync("complete");
    var nurseToken = await LoginPendingNurseAsync(registration.Email);
    var nurseClient = CreateAuthorizedClient(nurseToken);

    var blockedResponse = await nurseClient.GetAsync("/api/care-requests");
    Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);

    var adminClient = CreateAdminClient();
    var completionResponse = await adminClient.PutAsJsonAsync(
      $"/api/admin/nurse-profiles/{registration.UserId}/complete",
      new
      {
        name = "Laura",
        lastName = "Gomez",
        identificationNumber = "00111111111",
        phone = "8095550199",
        email = registration.Email,
        hireDate = "2026-03-21",
        specialty = "Cuidados intensivos",
        licenseId = "55",
        bankName = "Banco Central",
        accountNumber = "123456",
        category = "Senior"
      });

    completionResponse.EnsureSuccessStatusCode();
    var completionPayload = await completionResponse.Content.ReadFromJsonAsync<NurseProfileAdminDto>();
    Assert.NotNull(completionPayload);
    Assert.True(completionPayload!.UserIsActive);
    Assert.True(completionPayload.NurseProfileIsActive);
    Assert.Equal("Senior", completionPayload.Category);

    var unlockedResponse = await nurseClient.GetAsync("/api/care-requests");
    Assert.Equal(HttpStatusCode.OK, unlockedResponse.StatusCode);
  }

  [Fact]
  public async Task PUT_Update_Should_Edit_A_Completed_Nurse_Profile()
  {
    var adminClient = CreateAdminClient();
    var created = await CreateAdminNurseAsync(adminClient, "update", isOperationallyActive: true);

    var response = await adminClient.PutAsJsonAsync(
      $"/api/admin/nurse-profiles/{created.UserId}",
      new
      {
        name = "Mariela",
        lastName = "Santos",
        identificationNumber = "00122222222",
        phone = "8095550188",
        email = created.Email,
        hireDate = "2026-03-22",
        specialty = "Cuidado geriatrico",
        licenseId = "77",
        bankName = "Banco Nacional",
        accountNumber = "654321",
        category = "Lider"
      });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<NurseProfileAdminDto>();
    Assert.NotNull(payload);
    Assert.Equal("Cuidado geriatrico", payload!.Specialty);
    Assert.Equal("Lider", payload.Category);
    Assert.True(payload.IsAssignmentReady);
  }

  [Fact]
  public async Task PUT_Complete_Should_Return_BadRequest_For_Invalid_Field_Formats()
  {
    var registration = await RegisterPendingNurseAsync("invalid-complete");
    var adminClient = CreateAdminClient();

    var response = await adminClient.PutAsJsonAsync(
      $"/api/admin/nurse-profiles/{registration.UserId}/complete",
      new
      {
        name = "Laura2",
        lastName = "Gomez",
        identificationNumber = "0011111111",
        phone = "809555019A",
        email = registration.Email,
        hireDate = "2026-03-21",
        specialty = "Cuidados intensivos",
        licenseId = "55A",
        bankName = "Banco 123",
        accountNumber = "123-456",
        category = "Senior"
      });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task PUT_OperationalAccess_Should_Deactivate_Nurse_And_Block_Operational_Screens()
  {
    var registration = await RegisterPendingNurseAsync("deactivate");
    var adminClient = CreateAdminClient();
    await CompletePendingNurseAsync(adminClient, registration.UserId, registration.Email);

    var nurseToken = await LoginPendingNurseAsync(registration.Email);
    var nurseClient = CreateAuthorizedClient(nurseToken);

    var initialResponse = await nurseClient.GetAsync("/api/care-requests");
    Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

    var deactivateResponse = await adminClient.PutAsJsonAsync(
      $"/api/admin/nurse-profiles/{registration.UserId}/operational-access",
      new
      {
        isOperationallyActive = false
      });

    deactivateResponse.EnsureSuccessStatusCode();
    var payload = await deactivateResponse.Content.ReadFromJsonAsync<NurseProfileAdminDto>();
    Assert.NotNull(payload);
    Assert.False(payload!.UserIsActive);
    Assert.False(payload.NurseProfileIsActive);
    Assert.False(payload.IsPendingReview);
    Assert.False(payload.IsAssignmentReady);

    var blockedResponse = await nurseClient.GetAsync("/api/care-requests");
    Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
  }

  private HttpClient CreateAuthorizedClient(string token)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
  }

  private async Task<(Guid UserId, string Email)> RegisterPendingNurseAsync(string scenario)
  {
    var client = _factory.CreateClient();
    var email = $"{scenario}-{Guid.NewGuid():N}@nursingcare.local";

    var response = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Martinez",
      identificationNumber = "00133445567",
      phone = "8095550103",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Atencion domiciliaria",
      bankName = "Banco Central",
      profileType = 1
    });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(payload);
    return (payload!.UserId, payload.Email);
  }

  private async Task<string> RegisterAndLoginPendingNurseAsync(string scenario)
  {
    var registration = await RegisterPendingNurseAsync(scenario);
    return await LoginPendingNurseAsync(registration.Email);
  }

  private async Task<NurseProfileAdminDto> CreateAdminNurseAsync(
    HttpClient adminClient,
    string scenario,
    bool isOperationallyActive)
  {
    var email = $"{scenario}-{Guid.NewGuid():N}@nursingcare.local";
    var response = await adminClient.PostAsJsonAsync("/api/admin/nurse-profiles", new
    {
      name = "Laura",
      lastName = "Gomez",
      identificationNumber = "00111111111",
      phone = "8095550199",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Cuidados intensivos",
      licenseId = "55",
      bankName = "Banco Central",
      accountNumber = "123456",
      category = "Senior",
      isOperationallyActive
    });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<NurseProfileAdminDto>();
    Assert.NotNull(payload);
    return payload!;
  }

  private async Task CompletePendingNurseAsync(HttpClient adminClient, Guid userId, string email)
  {
    var response = await adminClient.PutAsJsonAsync(
      $"/api/admin/nurse-profiles/{userId}/complete",
      new
      {
        name = "Laura",
        lastName = "Gomez",
        identificationNumber = "00111111111",
        phone = "8095550199",
        email,
        hireDate = "2026-03-21",
        specialty = "Cuidados intensivos",
        licenseId = "55",
        bankName = "Banco Central",
        accountNumber = "123456",
        category = "Senior"
      });

    response.EnsureSuccessStatusCode();
  }

  private async Task<string> LoginPendingNurseAsync(string email)
  {
    var client = _factory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(payload);
    return payload!.Token;
  }

  private sealed class PendingNurseProfileDto
  {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Specialty { get; set; }
  }

  private sealed class WorkloadDto
  {
    public int TotalAssignedCareRequests { get; set; }
  }

  private sealed class NurseProfileSummaryDto
  {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsAssignmentReady { get; set; }
    public WorkloadDto Workload { get; set; } = new();
  }

  private sealed class NurseProfileAdminDto
  {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool UserIsActive { get; set; }
    public bool NurseProfileIsActive { get; set; }
    public bool IsPendingReview { get; set; }
    public bool IsAssignmentReady { get; set; }
    public bool HasHistoricalCareRequests { get; set; }
    public string? Specialty { get; set; }
    public string? Category { get; set; }
  }

  private sealed class AuthResponseDto
  {
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
  }
}

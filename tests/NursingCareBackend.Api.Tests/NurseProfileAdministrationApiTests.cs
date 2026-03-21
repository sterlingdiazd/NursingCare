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
    Assert.Equal("Home Care", payload.Specialty);
    Assert.False(payload.NurseProfileIsActive);
  }

  [Fact]
  public async Task PUT_Complete_Should_Finalize_Nurse_Profile_And_Enable_Operational_Access()
  {
    var registration = await RegisterPendingNurseAsync("complete");
    var nurseToken = await LoginPendingNurseAsync(registration.Email);
    var nurseClient = _factory.CreateClient();
    nurseClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);

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
        specialty = "Critical Care",
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
        specialty = "Critical Care",
        licenseId = "55A",
        bankName = "Banco 123",
        accountNumber = "123-456",
        category = "Senior"
      });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));
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
      specialty = "Home Care",
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
  }

  private sealed class NurseProfileAdminDto
  {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool UserIsActive { get; set; }
    public bool NurseProfileIsActive { get; set; }
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

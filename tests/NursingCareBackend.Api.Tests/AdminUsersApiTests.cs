using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminUsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AdminUsersApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GET_AdminUsers_Should_Search_And_Filter_By_Role_Profile_And_Status()
  {
    var clientAccount = await RegisterClientAsync($"admin-users-client-{Guid.NewGuid():N}");
    var nurseAccount = await RegisterPendingNurseAsync($"admin-users-nurse-{Guid.NewGuid():N}");
    var adminClient = CreateAdminClient();

    var clientResponse = await adminClient.GetAsync(
      $"/api/admin/users?search={Uri.EscapeDataString(clientAccount.Email)}&role=Client&profileType=Client&status=Active");

    clientResponse.EnsureSuccessStatusCode();
    var clientPayload = await clientResponse.Content.ReadFromJsonAsync<List<AdminUserListItemDto>>();

    Assert.NotNull(clientPayload);
    Assert.Contains(clientPayload!, item =>
      item.Id == clientAccount.UserId
      && item.Email == clientAccount.Email
      && item.ProfileType == "Client"
      && item.AccountStatus == "Active"
      && item.RoleNames.SequenceEqual(["Client"]));
    Assert.DoesNotContain(clientPayload!, item => item.Id == nurseAccount.UserId);

    var nurseResponse = await adminClient.GetAsync(
      $"/api/admin/users?search={Uri.EscapeDataString(nurseAccount.Email)}&role=Nurse&profileType=Nurse&status=AdminReview");

    nurseResponse.EnsureSuccessStatusCode();
    var nursePayload = await nurseResponse.Content.ReadFromJsonAsync<List<AdminUserListItemDto>>();

    Assert.NotNull(nursePayload);
    Assert.Contains(nursePayload!, item =>
      item.Id == nurseAccount.UserId
      && item.Email == nurseAccount.Email
      && item.ProfileType == "Nurse"
      && item.AccountStatus == "AdminReview"
      && item.RequiresAdminReview
      && item.RoleNames.SequenceEqual(["Nurse"]));
  }

  [Fact]
  public async Task GET_AdminUserDetail_Should_Return_Related_Profile_Data_And_Session_Counts()
  {
    var scenario = $"admin-user-detail-{Guid.NewGuid():N}";
    var nurseAccount = await RegisterCompletedNurseAsync($"{scenario}-nurse");
    var clientAccount = await RegisterClientAsync($"{scenario}-client");
    var careRequestId = await CreateCareRequestAsClientAsync(clientAccount.Token, $"{scenario}-solicitud");
    await AssignCareRequestAsync(careRequestId, nurseAccount.UserId);

    var adminClient = CreateAdminClient();
    var nurseResponse = await adminClient.GetAsync($"/api/admin/users/{nurseAccount.UserId}");

    nurseResponse.EnsureSuccessStatusCode();
    var nursePayload = await nurseResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>();

    Assert.NotNull(nursePayload);
    Assert.Equal(nurseAccount.UserId, nursePayload!.Id);
    Assert.Equal("Nurse", nursePayload.ProfileType);
    Assert.Equal("Active", nursePayload.AccountStatus);
    Assert.Equal(1, nursePayload.ActiveRefreshTokenCount);
    Assert.True(nursePayload.HasOperationalHistory);
    Assert.Equal(new[] { "Admin", "Nurse" }, nursePayload.AllowedRoleNames);
    Assert.NotNull(nursePayload.NurseProfile);
    Assert.Null(nursePayload.ClientProfile);
    Assert.True(nursePayload.NurseProfile!.IsActive);
    Assert.Equal("Atencion domiciliaria", nursePayload.NurseProfile.Specialty);
    Assert.Equal("Senior", nursePayload.NurseProfile.Category);
    Assert.Equal(1, nursePayload.NurseProfile.AssignedCareRequestsCount);

    var clientResponse = await adminClient.GetAsync($"/api/admin/users/{clientAccount.UserId}");

    clientResponse.EnsureSuccessStatusCode();
    var clientPayload = await clientResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>();

    Assert.NotNull(clientPayload);
    Assert.Equal(clientAccount.UserId, clientPayload!.Id);
    Assert.Equal("Client", clientPayload.ProfileType);
    Assert.Equal("Active", clientPayload.AccountStatus);
    Assert.Equal(1, clientPayload.ActiveRefreshTokenCount);
    Assert.True(clientPayload.HasOperationalHistory);
    Assert.Equal(new[] { "Admin", "Client" }, clientPayload.AllowedRoleNames);
    Assert.Null(clientPayload.NurseProfile);
    Assert.NotNull(clientPayload.ClientProfile);
    Assert.Equal(1, clientPayload.ClientProfile!.OwnedCareRequestsCount);
  }

  [Fact]
  public async Task PUT_AdminUser_Should_Update_Identity_Data()
  {
    var account = await RegisterClientAsync($"admin-user-update-{Guid.NewGuid():N}");
    var adminClient = CreateAdminClient();

    var response = await adminClient.PutAsJsonAsync($"/api/admin/users/{account.UserId}", new
    {
      name = "Mariela",
      lastName = "Santos",
      identificationNumber = "00111222333",
      phone = "8095550444",
      email = $"actualizada-{Guid.NewGuid():N}@nursingcare.local"
    });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>();

    Assert.NotNull(payload);
    Assert.Equal("Mariela Santos", payload!.DisplayName);
    Assert.Equal("Mariela", payload.Name);
    Assert.Equal("Santos", payload.LastName);
    Assert.Equal("00111222333", payload.IdentificationNumber);
    Assert.Equal("8095550444", payload.Phone);
    Assert.StartsWith("actualizada-", payload.Email, StringComparison.Ordinal);
  }

  [Fact]
  public async Task PUT_AdminUser_Should_Reject_Invalid_Identity_Data()
  {
    var account = await RegisterClientAsync($"admin-user-invalid-{Guid.NewGuid():N}");
    var adminClient = CreateAdminClient();

    var response = await adminClient.PutAsJsonAsync($"/api/admin/users/{account.UserId}", new
    {
      name = "Mario2",
      lastName = "Lopez",
      identificationNumber = "00122334455",
      phone = "8095550188",
      email = account.Email
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(payload);
    Assert.Equal("Solicitud invalida", payload!.Title);
    Assert.Equal("El nombre solo acepta letras y espacios.", payload.Detail);
  }

  [Fact]
  public async Task PUT_AdminUserRoles_Should_Update_Roles_And_Enforce_Profile_Policy()
  {
    var account = await RegisterClientAsync($"admin-user-roles-{Guid.NewGuid():N}");
    var adminClient = CreateAdminClient();

    var updateResponse = await adminClient.PutAsJsonAsync($"/api/admin/users/{account.UserId}/roles", new
    {
      roleNames = new[] { "Admin", "Client" }
    });

    updateResponse.EnsureSuccessStatusCode();
    var updated = await updateResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>();

    Assert.NotNull(updated);
    Assert.Equal(new[] { "Admin", "Client" }, updated!.RoleNames);

    var invalidResponse = await adminClient.PutAsJsonAsync($"/api/admin/users/{account.UserId}/roles", new
    {
      roleNames = new[] { "Nurse" }
    });

    Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

    var invalidPayload = await invalidResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(invalidPayload);
    Assert.Equal(
      "El rol de enfermeria solo puede asignarse a perfiles de enfermeria.",
      invalidPayload!.Detail);
  }

  [Fact]
  public async Task PUT_AdminUserActiveState_Should_Deactivate_User_And_Revoke_Sessions()
  {
    var account = await RegisterClientAsync($"admin-user-active-{Guid.NewGuid():N}");
    var adminClient = CreateAdminClient();

    var response = await adminClient.PutAsJsonAsync($"/api/admin/users/{account.UserId}/active-state", new
    {
      isActive = false
    });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>();

    Assert.NotNull(payload);
    Assert.False(payload!.IsActive);
    Assert.Equal("Inactive", payload.AccountStatus);
    Assert.Equal(0, payload.ActiveRefreshTokenCount);

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var revokedTokens = await dbContext.RefreshTokens
      .CountAsync(item => item.UserId == account.UserId && item.RevokedAtUtc != null);

    Assert.Equal(2, revokedTokens);
  }

  [Fact]
  public async Task POST_AdminUserInvalidateSessions_Should_Revoke_Active_RefreshTokens()
  {
    var account = await RegisterClientAsync($"admin-user-sessions-{Guid.NewGuid():N}");
    var adminClient = CreateAdminClient();

    var response = await adminClient.PostAsync($"/api/admin/users/{account.UserId}/invalidate-sessions", null);

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminUserSessionInvalidationDto>();

    Assert.NotNull(payload);
    Assert.Equal(account.UserId, payload!.UserId);
    Assert.Equal(1, payload.RevokedActiveSessionCount);

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var activeTokens = await dbContext.RefreshTokens
      .CountAsync(item => item.UserId == account.UserId && item.RevokedAtUtc == null && item.ExpiresAtUtc > DateTime.UtcNow);

    Assert.Equal(0, activeTokens);
  }

  [Fact]
  public async Task AdminUsers_Endpoints_Should_Reject_Non_Admin_Users()
  {
    var account = await RegisterClientAsync($"admin-user-forbidden-{Guid.NewGuid():N}");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);

    var listResponse = await client.GetAsync("/api/admin/users");
    var detailResponse = await client.GetAsync($"/api/admin/users/{account.UserId}");

    Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, detailResponse.StatusCode);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
  }

  private async Task<UserSession> RegisterClientAsync(string scenario)
  {
    var email = $"{scenario}@nursingcare.local";
    var client = _factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Carla",
      lastName = "Jimenez",
      identificationNumber = "00122334456",
      phone = "8095550101",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      profileType = 0
    });

    registerResponse.EnsureSuccessStatusCode();
    var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(registered);

    var loginToken = await LoginAsync(email);
    return new UserSession(registered!.UserId, email, loginToken);
  }

  private async Task<UserSession> RegisterPendingNurseAsync(string scenario)
  {
    var email = $"{scenario}@nursingcare.local";
    var client = _factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Laura",
      lastName = "Gomez",
      identificationNumber = "00111111111",
      phone = "8095550199",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Atencion domiciliaria",
      bankName = "Banco Central",
      profileType = 1
    });

    registerResponse.EnsureSuccessStatusCode();
    var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(registered);

    return new UserSession(registered!.UserId, email, registered.Token);
  }

  private async Task<UserSession> RegisterCompletedNurseAsync(string scenario)
  {
    var email = $"{scenario}@nursingcare.local";
    var client = _factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
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

    registerResponse.EnsureSuccessStatusCode();
    var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(registered);

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var completeResponse = await client.PutAsJsonAsync(
      $"/api/admin/nurse-profiles/{registered!.UserId}/complete",
      new
      {
        name = "Luisa",
        lastName = "Martinez",
        identificationNumber = "00133445567",
        phone = "8095550103",
        email,
        hireDate = "2026-03-21",
        specialty = "Atencion domiciliaria",
        licenseId = "01",
        bankName = "Banco Central",
        accountNumber = "12345",
        category = "Senior"
      });

    completeResponse.EnsureSuccessStatusCode();

    var loginToken = await LoginAsync(email);
    return new UserSession(registered.UserId, email, loginToken);
  }

  private async Task<string> LoginAsync(string email)
  {
    var client = _factory.CreateClient();
    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var payload = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(payload);
    return payload!.Token;
  }

  private async Task<Guid> CreateCareRequestAsClientAsync(
    string clientToken,
    string description)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = description,
      careRequestType = "domicilio_24h",
      unit = 1,
      careRequestDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
    });

    createResponse.EnsureSuccessStatusCode();
    var payload = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
    Assert.NotNull(payload);
    return payload!.Id;
  }

  private async Task AssignCareRequestAsync(Guid careRequestId, Guid nurseUserId)
  {
    var adminClient = CreateAdminClient();
    var response = await adminClient.PutAsJsonAsync($"/api/care-requests/{careRequestId}/assignment", new
    {
      assignedNurse = nurseUserId
    });

    response.EnsureSuccessStatusCode();
  }

  private sealed record UserSession(Guid UserId, string Email, string Token);

  private sealed class AdminUserListItemDto
  {
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string ProfileType { get; set; } = string.Empty;
    public List<string> RoleNames { get; set; } = [];
    public string AccountStatus { get; set; } = string.Empty;
    public bool RequiresAdminReview { get; set; }
  }

  private sealed class AdminUserDetailDto
  {
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? IdentificationNumber { get; set; }
    public string? Phone { get; set; }
    public string ProfileType { get; set; } = string.Empty;
    public List<string> RoleNames { get; set; } = [];
    public List<string> AllowedRoleNames { get; set; } = [];
    public bool IsActive { get; set; }
    public string AccountStatus { get; set; } = string.Empty;
    public bool HasOperationalHistory { get; set; }
    public int ActiveRefreshTokenCount { get; set; }
    public NurseProfileDto? NurseProfile { get; set; }
    public ClientProfileDto? ClientProfile { get; set; }
  }

  private sealed class NurseProfileDto
  {
    public bool IsActive { get; set; }
    public string? Specialty { get; set; }
    public string? Category { get; set; }
    public int AssignedCareRequestsCount { get; set; }
  }

  private sealed class ClientProfileDto
  {
    public int OwnedCareRequestsCount { get; set; }
  }

  private sealed class AdminUserSessionInvalidationDto
  {
    public Guid UserId { get; set; }
    public int RevokedActiveSessionCount { get; set; }
  }

  private sealed class ProblemDetailsDto
  {
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private sealed class AuthResponseDto
  {
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
  }
}

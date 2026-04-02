using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminAccountsApiTests
{
  [Fact]
  public async Task POST_AdminAccounts_Should_Create_Admin_User_And_Write_Audit_Record()
  {
    using var factory = new CustomWebApplicationFactory();
    var adminSession = await CreateBootstrapAdminAsync(factory, $"admin-create-root-{Guid.NewGuid():N}");

    var response = await adminSession.Client.PostAsJsonAsync("/api/admin/admin-accounts", new
    {
      name = "Mariela",
      lastName = "Rojas",
      identificationNumber = "00111222333",
      phone = "8095550199",
      email = $"admin-creada-{Guid.NewGuid():N}@nursingcare.local",
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>();
    Assert.NotNull(payload);
    Assert.Equal("ADMIN", payload!.ProfileType);
    Assert.Equal("Active", payload.AccountStatus);
    Assert.Equal(new[] { "ADMIN" }, payload.RoleNames);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var createdUser = await dbContext.Users
      .Include(user => user.UserRoles)
      .ThenInclude(userRole => userRole.Role)
      .SingleAsync(user => user.Id == payload.Id);

    Assert.Equal(payload.Email, createdUser.Email);
    Assert.Contains(createdUser.UserRoles, userRole => userRole.Role.Name == "ADMIN");

    var auditLog = await dbContext.AuditLogs
      .SingleAsync(item => item.Action == "AdminAccountCreated" && item.EntityId == payload.Id.ToString());

    Assert.Equal(adminSession.UserId, auditLog.ActorUserId);
    Assert.Equal("ADMIN", auditLog.ActorRole);
  }

  [Fact]
  public async Task POST_AdminAccounts_Should_Reject_Non_Admin_Users()
  {
    using var factory = new CustomWebApplicationFactory();
    var client = factory.CreateClient();
    var email = $"admin-account-forbidden-{Guid.NewGuid():N}@nursingcare.local";

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Perez",
      identificationNumber = "00133445566",
      phone = "8095550166",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    registerResponse.EnsureSuccessStatusCode();
    var session = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);

    var response = await client.PostAsJsonAsync("/api/admin/admin-accounts", new
    {
      name = "Mariela",
      lastName = "Rojas",
      identificationNumber = "00111222333",
      phone = "8095550199",
      email = $"admin-no-{Guid.NewGuid():N}@nursingcare.local",
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private static async Task<BootstrapAdminSession> CreateBootstrapAdminAsync(
    CustomWebApplicationFactory factory,
    string scenario)
  {
    var adminClient = factory.CreateClient();
    adminClient.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(factory.Services));

    return new BootstrapAdminSession(adminClient, Guid.Parse("00000000-0000-0000-0000-000000000001"));
  }

  private sealed record BootstrapAdminSession(HttpClient Client, Guid UserId);

  private sealed class AuthResponseDto
  {
    public string Token { get; set; } = string.Empty;
  }

  private sealed class AdminUserDetailDto
  {
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string ProfileType { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public string[] RoleNames { get; set; } = [];
  }
}

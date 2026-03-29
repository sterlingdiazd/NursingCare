using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminActionItemsApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AdminActionItemsApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GET_AdminActionItems_Should_Return_Derived_AdminQueue_Items()
  {
    var pendingNurse = await RegisterPendingNurseAsync("queue-pending-nurse");
    var (_, readyNurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "queue-ready-nurse");
    var (_, blockedNurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "queue-blocked-nurse");
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "queue-client");

    var unassignedRequestId = await CreateCareRequestAsClientAsync(
      clientToken,
      "Solicitud sin enfermera",
      DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
    var readyForApprovalRequestId = await CreateCareRequestAsClientAsync(clientToken, "Solicitud lista para aprobacion");
    var blockedRequestId = await CreateCareRequestAsClientAsync(clientToken, "Solicitud con asignacion invalida");

    await AssignCareRequestAsync(readyForApprovalRequestId, readyNurseUserId);
    await AssignCareRequestAsync(blockedRequestId, blockedNurseUserId);
    await DeactivateUserAsync(blockedNurseUserId);

    var inconsistentUserId = await CreateUserRequiringManualInterventionAsync();
    var adminClient = CreateAdminClient();

    var response = await adminClient.GetAsync("/api/admin/action-items");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<List<ActionItemDto>>();

    Assert.NotNull(payload);
    Assert.Contains(payload!, item =>
      item.EntityType == "NurseProfile"
      && item.EntityIdentifier == pendingNurse.UserId.ToString()
      && item.DeepLinkPath == $"/admin/nurse-profiles?view=pending&userId={pendingNurse.UserId}");
    Assert.Contains(payload!, item =>
      item.EntityType == "CareRequest"
      && item.EntityIdentifier == unassignedRequestId.ToString()
      && item.DeepLinkPath == $"/admin/care-requests?view=unassigned&selected={unassignedRequestId}");
    Assert.Contains(payload!, item =>
      item.EntityType == "CareRequest"
      && item.EntityIdentifier == readyForApprovalRequestId.ToString()
      && item.DeepLinkPath == $"/admin/care-requests?view=pending-approval&selected={readyForApprovalRequestId}");
    Assert.Contains(payload!, item =>
      item.EntityType == "CareRequest"
      && item.EntityIdentifier == blockedRequestId.ToString()
      && item.DeepLinkPath == $"/admin/care-requests?selected={blockedRequestId}"
      && item.Severity == "High");
    Assert.Contains(payload!, item =>
      item.EntityType == "UserAccount"
      && item.EntityIdentifier == inconsistentUserId.ToString()
      && item.DeepLinkPath == $"/admin/users/{inconsistentUserId}");
    Assert.Contains(payload!, item =>
      item.EntityType == "SystemIssue"
      && item.EntityIdentifier == "overdue-backlog"
      && item.DeepLinkPath == "/admin/care-requests?view=overdue");
    Assert.All(payload!, item =>
    {
      Assert.False(string.IsNullOrWhiteSpace(item.Id));
      Assert.Contains(item.Severity, new[] { "High", "Medium", "Low" });
      Assert.Contains(item.State, new[] { "Unread", "Pending" });
      Assert.False(string.IsNullOrWhiteSpace(item.Summary));
      Assert.False(string.IsNullOrWhiteSpace(item.RequiredAction));
      Assert.False(string.IsNullOrWhiteSpace(item.DeepLinkPath));
    });
  }

  [Fact]
  public async Task GET_AdminActionItems_Should_Reject_Non_Admin_Users()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "queue-forbidden");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var response = await client.GetAsync("/api/admin/action-items");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
  }

  private async Task<(Guid UserId, string Email)> RegisterPendingNurseAsync(string scenario)
  {
    var client = _factory.CreateClient();
    var email = $"{scenario}-{Guid.NewGuid():N}@nursingcare.local";

    var response = await client.PostAsJsonAsync("/api/auth/register", new
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

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(payload);
    return (payload!.UserId, payload.Email);
  }

  private async Task<Guid> CreateCareRequestAsClientAsync(
    string clientToken,
    string description,
    DateOnly? careRequestDate = null)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = description,
      careRequestType = "domicilio_24h",
      unit = 1,
      careRequestDate = careRequestDate?.ToString("yyyy-MM-dd")
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
    Assert.NotNull(created);
    return created!.Id;
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

  private async Task DeactivateUserAsync(Guid userId)
  {
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var user = await dbContext.Users
      .Include(item => item.NurseProfile)
      .FirstAsync(item => item.Id == userId);

    user.IsActive = false;
    await dbContext.SaveChangesAsync();
  }

  private async Task<Guid> CreateUserRequiringManualInterventionAsync()
  {
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = $"manual-{Guid.NewGuid():N}@nursingcare.local",
      ProfileType = UserProfileType.CLIENT,
      Name = "Mario",
      LastName = "Lopez",
      IdentificationNumber = "00122334455",
      Phone = "8095550177",
      PasswordHash = "hashed-password",
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow.AddHours(-3),
    };

    dbContext.Users.Add(user);
    await dbContext.SaveChangesAsync();

    return user.Id;
  }

  private sealed class ActionItemDto
  {
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityIdentifier { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string RequiredAction { get; set; } = string.Empty;
    public string? AssignedOwner { get; set; }
    public string DeepLinkPath { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; }
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private sealed class AuthResponseDto
  {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
  }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminNotificationsApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AdminNotificationsApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
    _factory.EnsureDatabaseInitialized();
  }

  [Fact]
  public async Task GET_AdminNotifications_Should_Reject_Non_Admin_Users()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"notifications-forbidden-{Guid.NewGuid():N}");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var response = await client.GetAsync("/api/admin/notifications");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Admin_User_Should_Exist_In_Database()
  {
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareBackend.Infrastructure.Persistence.NursingCareDbContext>();
    
    var adminUsers = await dbContext.Users
      .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == "ADMIN"))
      .ToListAsync();
    
    Assert.NotEmpty(adminUsers);
  }

  [Fact]
  public async Task NotificationCenter_Should_Expose_And_Update_Read_State()
  {
    var adminClient = CreateAdminClient();
    var scenario = $"notifications-{Guid.NewGuid():N}";

    var registerResponse = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
    {
      name = "Laura",
      lastName = "Gomez",
      identificationNumber = "00111111111",
      phone = "8095550199",
      email = $"{scenario}@nursingcare.local",
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Atencion domiciliaria",
      bankName = "Banco Central",
      profileType = 1
    });
    registerResponse.EnsureSuccessStatusCode();

    var listResponse = await adminClient.GetAsync("/api/admin/notifications?unreadOnly=true");
    listResponse.EnsureSuccessStatusCode();
    var items = await listResponse.Content.ReadFromJsonAsync<List<AdminNotificationDto>>();
    Assert.NotNull(items);

    var nurseRegistration = items!.FirstOrDefault(item => item.Category == "nurse_registration_created");
    Assert.NotNull(nurseRegistration);
    Assert.False(nurseRegistration!.ReadAtUtc.HasValue);
    Assert.Equal("NurseProfile", nurseRegistration.EntityType);
    Assert.False(string.IsNullOrWhiteSpace(nurseRegistration.DeepLinkPath));

    var markReadResponse = await adminClient.PostAsync($"/api/admin/notifications/{nurseRegistration.Id}/read", null);
    Assert.Equal(HttpStatusCode.NoContent, markReadResponse.StatusCode);

    var summaryResponse = await adminClient.GetAsync("/api/admin/notifications/summary");
    summaryResponse.EnsureSuccessStatusCode();
    var summary = await summaryResponse.Content.ReadFromJsonAsync<AdminNotificationSummaryDto>();
    Assert.NotNull(summary);
    Assert.True(summary!.Total >= 2);
    Assert.True(summary.Unread <= summary.Total);
  }

  [Fact]
  public async Task CareRequest_Creation_Should_Generate_Notification_And_DeepLink()
  {
    var scenario = $"notifications-care-request-{Guid.NewGuid():N}";
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, scenario);
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = $"Solicitud {scenario}",
      careRequestType = "domicilio_24h",
      unit = 1
    });
    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
    Assert.NotNull(created);

    var adminClient = CreateAdminClient();
    var listResponse = await adminClient.GetAsync("/api/admin/notifications?unreadOnly=true");
    listResponse.EnsureSuccessStatusCode();
    var items = await listResponse.Content.ReadFromJsonAsync<List<AdminNotificationDto>>();
    Assert.NotNull(items);
    Assert.Contains(items!, item =>
      item.Category == "care_request_created"
      && item.EntityType == "CareRequest"
      && item.EntityId == created!.Id.ToString()
      && item.DeepLinkPath == $"/admin/care-requests/{created.Id}");
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private sealed class AdminNotificationDto
  {
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DeepLinkPath { get; set; }
    public DateTime? ReadAtUtc { get; set; }
  }

  private sealed class AdminNotificationSummaryDto
  {
    public int Total { get; set; }
    public int Unread { get; set; }
  }
}

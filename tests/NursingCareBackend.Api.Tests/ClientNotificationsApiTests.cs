using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Notifications;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class ClientNotificationsApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public ClientNotificationsApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
    _factory.EnsureDatabaseInitialized();
  }

  [Fact]
  public async Task GET_ClientNotifications_Should_Reject_Admin_Users()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

    var response = await client.GetAsync("/api/client/notifications");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task NotificationCenter_Should_Expose_Only_Owned_Notifications_And_Update_State()
  {
    var scenario = $"client-notifications-{Guid.NewGuid():N}";
    var (clientToken, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, scenario);
    var (_, otherClientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"{scenario}-other");
    var ownedNotificationId = Guid.NewGuid();

    using (var scope = _factory.Services.CreateScope())
    {
      var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
      db.UserNotifications.AddRange(
        new UserNotification
        {
          Id = ownedNotificationId,
          RecipientUserId = clientUserId,
          Category = "care_request_approved",
          Severity = "Medium",
          Title = "Solicitud aprobada",
          Body = "Tu solicitud fue aprobada.",
          EntityType = "CareRequest",
          EntityId = Guid.NewGuid().ToString(),
          DeepLinkPath = "/care-requests/test",
          Source = "Solicitudes",
          RequiresAction = false,
          IsDismissed = false,
          CreatedAtUtc = DateTime.UtcNow,
          CreatedBySystem = true,
        },
        new UserNotification
        {
          Id = Guid.NewGuid(),
          RecipientUserId = otherClientUserId,
          Category = "payment_confirmed",
          Severity = "Medium",
          Title = "Pago confirmado",
          Body = "Confirmamos el pago.",
          Source = "Cobros",
          RequiresAction = false,
          IsDismissed = false,
          CreatedAtUtc = DateTime.UtcNow,
          CreatedBySystem = true,
        });
      await db.SaveChangesAsync();
    }

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var listResponse = await client.GetAsync("/api/client/notifications?status=Unread&pageSize=100");
    listResponse.EnsureSuccessStatusCode();
    var page = await listResponse.Content.ReadFromJsonAsync<UserNotificationPageDto>();
    Assert.NotNull(page);
    Assert.Single(page!.Items);
    Assert.Equal(ownedNotificationId, page.Items[0].Id);
    Assert.False(page.Items[0].ReadAtUtc.HasValue);

    var readResponse = await client.PostAsync($"/api/client/notifications/{ownedNotificationId}/read", null);
    Assert.Equal(HttpStatusCode.NoContent, readResponse.StatusCode);

    var summaryResponse = await client.GetAsync("/api/client/notifications/summary");
    summaryResponse.EnsureSuccessStatusCode();
    var summary = await summaryResponse.Content.ReadFromJsonAsync<UserNotificationSummaryDto>();
    Assert.NotNull(summary);
    Assert.Equal(1, summary!.Total);
    Assert.Equal(0, summary.Unread);

    var dismissResponse = await client.PostAsync($"/api/client/notifications/{ownedNotificationId}/dismiss", null);
    Assert.Equal(HttpStatusCode.NoContent, dismissResponse.StatusCode);

    var archiveResponse = await client.PostAsync($"/api/client/notifications/{ownedNotificationId}/archive", null);
    Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

    var activeResponse = await client.GetAsync("/api/client/notifications?status=Active&pageSize=100");
    activeResponse.EnsureSuccessStatusCode();
    var activePage = await activeResponse.Content.ReadFromJsonAsync<UserNotificationPageDto>();
    Assert.NotNull(activePage);
    Assert.Empty(activePage!.Items);
  }

  [Fact]
  public async Task CareRequest_Approval_Should_Generate_Client_Notification_And_Outbox_Row()
  {
    var scenario = $"client-notifications-approval-{Guid.NewGuid():N}";
    var (clientToken, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, scenario);
    var (_, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, $"{scenario}-nurse");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, $"Solicitud {scenario}");
    await AssignCareRequestAsync(createdId, nurseUserId);

    var adminClient = CreateAdminClient();
    var approveResponse = await adminClient.PostAsync($"/api/care-requests/{createdId}/approve", null);
    approveResponse.EnsureSuccessStatusCode();

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
    var listResponse = await client.GetAsync("/api/client/notifications?status=Unread&pageSize=100");
    listResponse.EnsureSuccessStatusCode();
    var page = await listResponse.Content.ReadFromJsonAsync<UserNotificationPageDto>();

    Assert.NotNull(page);
    var notification = Assert.Single(page!.Items, item =>
      item.Category == "care_request_approved"
      && item.EntityType == "CareRequest"
      && item.EntityId == createdId.ToString()
      && item.DeepLinkPath == $"/care-requests/{createdId}");
    Assert.Equal("Solicitud aprobada", notification.Title);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var outbox = await db.NotificationOutbox
      .AsNoTracking()
      .FirstOrDefaultAsync(row =>
        row.NotificationId == notification.Id
        && row.RecipientUserId == clientUserId);

    Assert.NotNull(outbox);
    Assert.Equal(NotificationOutboxKind.User, outbox!.Kind);
    Assert.Equal("Solicitudes", outbox.Source);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
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
    var adminClient = CreateAdminClient();
    var response = await adminClient.PutAsJsonAsync($"/api/care-requests/{careRequestId}/assignment", new
    {
      assignedNurse = nurseUserId
    });

    response.EnsureSuccessStatusCode();
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private sealed class UserNotificationDto
  {
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DeepLinkPath { get; set; }
    public DateTime? ReadAtUtc { get; set; }
  }

  private sealed class UserNotificationSummaryDto
  {
    public int Total { get; set; }
    public int Unread { get; set; }
  }

  private sealed class UserNotificationPageDto
  {
    public List<UserNotificationDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
  }
}

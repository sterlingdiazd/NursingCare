using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminDashboardApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AdminDashboardApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GET_AdminDashboard_Should_Return_AdminPortalCounts()
  {
    await RegisterPendingNurseAsync("dashboard-pending-nurse");
    var (_, activeNurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "dashboard-active-nurse");
    var (firstClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "dashboard-client-1");
    var (secondClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "dashboard-client-2");

    var overdueRequestId = await CreateCareRequestAsClientAsync(
      firstClientToken,
      "Solicitud atrasada",
      DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
    var waitingApprovalRequestId = await CreateCareRequestAsClientAsync(secondClientToken, "Solicitud lista para aprobacion");
    var approvedRequestId = await CreateCareRequestAsClientAsync(firstClientToken, "Solicitud aprobada sin cierre");
    var rejectedRequestId = await CreateCareRequestAsClientAsync(secondClientToken, "Solicitud rechazada hoy");

    await AssignCareRequestAsync(waitingApprovalRequestId, activeNurseUserId);
    await AssignCareRequestAsync(approvedRequestId, activeNurseUserId);
    await ApproveCareRequestAsync(approvedRequestId);
    await RejectCareRequestAsync(rejectedRequestId);

    var adminClient = CreateAdminClient();

    var response = await adminClient.GetAsync("/api/admin/dashboard");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminDashboardDto>();

    Assert.NotNull(payload);
    Assert.Equal(1, payload!.PendingNurseProfilesCount);
    Assert.Equal(1, payload.CareRequestsWaitingForAssignmentCount);
    Assert.Equal(1, payload.CareRequestsWaitingForApprovalCount);
    Assert.Equal(1, payload.CareRequestsRejectedTodayCount);
    Assert.Equal(1, payload.ApprovedCareRequestsStillIncompleteCount);
    Assert.Equal(1, payload.OverdueOrStaleRequestsCount);
    Assert.Equal(1, payload.ActiveNursesCount);
    Assert.Equal(2, payload.ActiveClientsCount);
    Assert.Equal(0, payload.UnreadAdminNotificationsCount);
    Assert.Empty(payload.HighSeverityAlerts);
    Assert.True(payload.GeneratedAtUtc > DateTime.UtcNow.AddMinutes(-5));

    Assert.NotEqual(Guid.Empty, overdueRequestId);
  }

  [Fact]
  public async Task GET_AdminDashboard_Should_Reject_Non_Admin_Users()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "dashboard-forbidden");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var response = await client.GetAsync("/api/admin/dashboard");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
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

  private async Task ApproveCareRequestAsync(Guid careRequestId)
  {
    var adminClient = CreateAdminClient();
    var response = await adminClient.PostAsync($"/api/care-requests/{careRequestId}/approve", null);
    response.EnsureSuccessStatusCode();
  }

  private async Task RejectCareRequestAsync(Guid careRequestId)
  {
    var adminClient = CreateAdminClient();
    var response = await adminClient.PostAsync($"/api/care-requests/{careRequestId}/reject", null);
    response.EnsureSuccessStatusCode();
  }

  private async Task RegisterPendingNurseAsync(string scenario)
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
  }

  private sealed class AdminDashboardDto
  {
    public int PendingNurseProfilesCount { get; set; }
    public int CareRequestsWaitingForAssignmentCount { get; set; }
    public int CareRequestsWaitingForApprovalCount { get; set; }
    public int CareRequestsRejectedTodayCount { get; set; }
    public int ApprovedCareRequestsStillIncompleteCount { get; set; }
    public int OverdueOrStaleRequestsCount { get; set; }
    public int ActiveNursesCount { get; set; }
    public int ActiveClientsCount { get; set; }
    public int UnreadAdminNotificationsCount { get; set; }
    public List<AdminDashboardAlertDto> HighSeverityAlerts { get; set; } = [];
    public DateTime GeneratedAtUtc { get; set; }
  }

  private sealed class AdminDashboardAlertDto
  {
    public string Id { get; set; } = string.Empty;
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminCareRequestsApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AdminCareRequestsApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GET_AdminCareRequests_Should_Filter_By_View_And_DateRange()
  {
    var scenario = $"admin-unassigned-{Guid.NewGuid():N}";
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"{scenario}-client");
    var (_, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, $"{scenario}-nurse");

    var unassignedId = await CreateCareRequestAsClientAsync(
      clientToken,
      $"{scenario}-sin-asignar",
      "domicilio_24h",
      today);
    var assignedId = await CreateCareRequestAsClientAsync(
      clientToken,
      $"{scenario}-asignada",
      "domicilio_24h",
      today.AddDays(1));
    await AssignCareRequestAsync(assignedId, nurseUserId);

    var adminClient = CreateAdminClient();
    var response = await adminClient.GetAsync($"/api/admin/care-requests?view=unassigned&scheduledTo={today:yyyy-MM-dd}");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<List<AdminCareRequestListItemDto>>();

    Assert.NotNull(payload);
    var matchingItems = payload!
      .Where(item => item.CareRequestDescription.StartsWith(scenario, StringComparison.Ordinal))
      .ToList();

    Assert.Single(matchingItems);
    Assert.Equal(unassignedId, matchingItems[0].Id);
    Assert.Null(matchingItems[0].AssignedNurseUserId);
  }

  [Fact]
  public async Task GET_AdminCareRequests_Should_Search_And_Sort_By_Value()
  {
    var scenario = $"admin-search-{Guid.NewGuid():N}";
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var (firstClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"{scenario}-client-1");
    var (secondClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"{scenario}-client-2");

    var lowerValueId = await CreateCareRequestAsClientAsync(
      firstClientToken,
      $"{scenario}-coincide-basico",
      "hogar_basico",
      today);
    var higherValueId = await CreateCareRequestAsClientAsync(
      secondClientToken,
      $"{scenario}-coincide-premium",
      "hogar_premium",
      today.AddDays(1));
    await CreateCareRequestAsClientAsync(
      secondClientToken,
      $"{scenario}-ignorar",
      "curas",
      today);

    var adminClient = CreateAdminClient();
    var response = await adminClient.GetAsync($"/api/admin/care-requests?search={scenario}-coincide&sort=value&scheduledFrom={today:yyyy-MM-dd}");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<List<AdminCareRequestListItemDto>>();

    Assert.NotNull(payload);
    var matchingItems = payload!
      .Where(item => item.CareRequestDescription.StartsWith($"{scenario}-coincide", StringComparison.Ordinal))
      .ToList();

    Assert.Equal(2, matchingItems.Count);
    Assert.Equal(higherValueId, matchingItems[0].Id);
    Assert.Equal(lowerValueId, matchingItems[1].Id);
    Assert.True(matchingItems[0].Total > matchingItems[1].Total);
  }

  [Fact]
  public async Task GET_AdminCareRequest_ById_Should_Return_PricingBreakdown_And_Timeline()
  {
    var scenario = $"admin-detail-{Guid.NewGuid():N}";
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"{scenario}-client");
    var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, $"{scenario}-nurse");
    var careRequestId = await CreateCareRequestAsClientAsync(
      clientToken,
      $"{scenario}-detalle",
      "domicilio_24h",
      today);

    await AssignCareRequestAsync(careRequestId, nurseUserId);
    await ApproveCareRequestAsync(careRequestId);

    var adminClient = CreateAdminClient();
    var response = await adminClient.GetAsync($"/api/admin/care-requests/{careRequestId}");

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<AdminCareRequestDetailDto>();

    Assert.NotNull(payload);
    Assert.Equal(careRequestId, payload!.Id);
    Assert.Equal("domicilio", payload.PricingBreakdown.Category);
    Assert.Equal(3500m, payload.PricingBreakdown.BasePrice);
    Assert.Equal(1.2m, payload.PricingBreakdown.CategoryFactor);
    Assert.Equal("local", payload.PricingBreakdown.DistanceFactor);
    Assert.Equal("estandar", payload.PricingBreakdown.ComplexityLevel);
    Assert.Equal("Approved", payload.Status);
    Assert.NotNull(payload.AssignedNurseDisplayName);
    Assert.Contains(payload.Timeline, item => item.Title == "Solicitud creada");
    Assert.Contains(payload.Timeline, item => item.Title == "Solicitud aprobada");

    var nurseClient = _factory.CreateClient();
    nurseClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var completeResponse = await nurseClient.PostAsync($"/api/care-requests/{careRequestId}/complete", null);
    completeResponse.EnsureSuccessStatusCode();

    var completedDetailResponse = await adminClient.GetAsync($"/api/admin/care-requests/{careRequestId}");
    completedDetailResponse.EnsureSuccessStatusCode();
    var completedPayload = await completedDetailResponse.Content.ReadFromJsonAsync<AdminCareRequestDetailDto>();

    Assert.NotNull(completedPayload);
    Assert.NotNull(completedPayload!.PayrollCompensation);
    Assert.True(completedPayload.PayrollCompensation!.NetCompensation > 0);
  }

  [Fact]
  public async Task GET_AdminCareRequests_Export_Should_Return_Csv_For_Filtered_Results()
  {
    var scenario = $"admin-export-{Guid.NewGuid():N}";
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"{scenario}-client");
    var careRequestId = await CreateCareRequestAsClientAsync(clientToken, $"{scenario}-exportable", "curas");

    var adminClient = CreateAdminClient();
    var response = await adminClient.GetAsync($"/api/admin/care-requests/export?search={scenario}-exportable");

    response.EnsureSuccessStatusCode();
    Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

    var csv = await response.Content.ReadAsStringAsync();
    Assert.Contains("Id,Estado,Cliente,CorreoCliente", csv);
    Assert.Contains(careRequestId.ToString(), csv);
  }

  [Fact]
  public async Task POST_AdminCareRequests_Should_Create_On_Behalf_Of_Client()
  {
    var scenario = $"admin-create-{Guid.NewGuid():N}";
    var (_, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"{scenario}-client");
    var adminClient = CreateAdminClient();

    var response = await adminClient.PostAsJsonAsync("/api/admin/care-requests", new
    {
      clientUserId,
      careRequestDescription = $"{scenario}-solicitud",
      careRequestType = "domicilio_24h",
      unit = 1,
      careRequestDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<CreateResponse>();
    Assert.NotNull(payload);

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var created = await dbContext.CareRequests.FirstAsync(item => item.Id == payload!.Id);

    Assert.Equal(clientUserId, created.UserID);
    Assert.Equal($"{scenario}-solicitud", created.Description);
  }

  [Fact]
  public async Task POST_AdminCareRequests_Should_Return_BadRequest_For_Invalid_Client()
  {
    var adminClient = CreateAdminClient();

    var response = await adminClient.PostAsJsonAsync("/api/admin/care-requests", new
    {
      clientUserId = Guid.NewGuid(),
      careRequestDescription = "solicitud invalida",
      careRequestType = "domicilio_24h",
      unit = 1
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
  }

  [Fact]
  public async Task POST_AdminCareRequests_Should_Reject_Admin_Only_Profiles_And_Hide_Them_From_Client_Options()
  {
    var adminClient = CreateAdminClient();
    var scenario = $"admin-only-profile-{Guid.NewGuid():N}";
    var (_, clientUserId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, scenario);

    var createAdminResponse = await adminClient.PostAsJsonAsync("/api/admin/admin-accounts", new
    {
      name = "Mariela",
      lastName = "Rojas",
      identificationNumber = "00111222333",
      phone = "8095550199",
      email = $"{scenario}-admin@nursingcare.local",
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    createAdminResponse.EnsureSuccessStatusCode();
    var createdAdmin = await createAdminResponse.Content.ReadFromJsonAsync<AdminAccountDto>();
    Assert.NotNull(createdAdmin);

    var optionsResponse = await adminClient.GetAsync($"/api/admin/care-requests/clients?search={Uri.EscapeDataString(scenario)}");
    optionsResponse.EnsureSuccessStatusCode();
    var optionsPayload = await optionsResponse.Content.ReadFromJsonAsync<List<AdminCareRequestClientOptionDto>>();

    Assert.NotNull(optionsPayload);
    Assert.Contains(optionsPayload!, item => item.UserId == clientUserId);
    Assert.DoesNotContain(optionsPayload!, item => item.UserId == createdAdmin!.Id);

    var createResponse = await adminClient.PostAsJsonAsync("/api/admin/care-requests", new
    {
      clientUserId = createdAdmin!.Id,
      careRequestDescription = "solicitud invalida para perfil administrativo",
      careRequestType = "domicilio_24h",
      unit = 1
    });

    Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
  }

  [Fact]
  public async Task GET_AdminCareRequests_Should_Reject_Non_Admin_Users()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, $"admin-forbidden-{Guid.NewGuid():N}");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var response = await client.GetAsync("/api/admin/care-requests");

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
    string careRequestType = "domicilio_24h",
    DateOnly? careRequestDate = null)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = description,
      careRequestType,
      unit = 1,
      careRequestDate = careRequestDate?.ToString("yyyy-MM-dd")
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

  private async Task ApproveCareRequestAsync(Guid careRequestId)
  {
    var adminClient = CreateAdminClient();
    var response = await adminClient.PostAsync($"/api/care-requests/{careRequestId}/approve", null);
    response.EnsureSuccessStatusCode();
  }

  private sealed class AdminCareRequestListItemDto
  {
    public Guid Id { get; set; }
    public Guid? AssignedNurseUserId { get; set; }
    public string CareRequestDescription { get; set; } = string.Empty;
    public decimal Total { get; set; }
  }

  private sealed class AdminCareRequestDetailDto
  {
    public Guid Id { get; set; }
    public string? AssignedNurseDisplayName { get; set; }
    public string Status { get; set; } = string.Empty;
    public AdminCareRequestPricingBreakdownDto PricingBreakdown { get; set; } = new();
    public AdminPayrollCompensationDto? PayrollCompensation { get; set; }
    public List<AdminCareRequestTimelineEventDto> Timeline { get; set; } = [];
  }

  private sealed class AdminPayrollCompensationDto
  {
    public decimal NetCompensation { get; set; }
  }

  private sealed class AdminCareRequestPricingBreakdownDto
  {
    public string Category { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal CategoryFactor { get; set; }
    public string? DistanceFactor { get; set; }
    public string? ComplexityLevel { get; set; }
  }

  private sealed class AdminCareRequestTimelineEventDto
  {
    public string Title { get; set; } = string.Empty;
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private sealed class AdminCareRequestClientOptionDto
  {
    public Guid UserId { get; set; }
  }

  private sealed class AdminAccountDto
  {
    public Guid Id { get; set; }
  }
}

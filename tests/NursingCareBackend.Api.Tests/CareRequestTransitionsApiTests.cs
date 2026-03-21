using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NursingCareBackend.Api.Tests;

public sealed class CareRequestTransitionsApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public CareRequestTransitionsApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task POST_Approve_Should_Transition_Request_To_Approved_For_Admin()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "approve-admin-client");
    var (_, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "approve-admin-nurse");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "Transition me");
    await AssignCareRequestAsync(createdId, nurseUserId);

    var client = CreateAdminClient();
    var approveResponse = await client.PostAsync($"/api/care-requests/{createdId}/approve", null);

    approveResponse.EnsureSuccessStatusCode();
    var payload = await approveResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(payload);
    Assert.Equal("Approved", payload!.Status);
    Assert.NotNull(payload.ApprovedAtUtc);
  }

  [Fact]
  public async Task POST_Approve_Should_Return_Forbidden_For_Nurse()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "approve-forbidden-client");
    var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "approve-forbidden");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "Admin only approve");
    await AssignCareRequestAsync(createdId, nurseUserId);

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var approveResponse = await client.PostAsync($"/api/care-requests/{createdId}/approve", null);

    Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
  }

  [Fact]
  public async Task POST_Reject_Should_Transition_Request_To_Rejected_For_Admin()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "reject-admin-client");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "Reject me");

    var client = CreateAdminClient();
    var rejectResponse = await client.PostAsync($"/api/care-requests/{createdId}/reject", null);

    rejectResponse.EnsureSuccessStatusCode();
    var payload = await rejectResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(payload);
    Assert.Equal("Rejected", payload!.Status);
    Assert.NotNull(payload.RejectedAtUtc);
  }

  [Fact]
  public async Task POST_Complete_Should_Transition_Approved_Request_To_Completed()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "complete-success-client");
    var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "complete-success");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "Complete me");
    await AssignCareRequestAsync(createdId, nurseUserId);

    var adminClient = CreateAdminClient();
    var approveResponse = await adminClient.PostAsync($"/api/care-requests/{createdId}/approve", null);
    approveResponse.EnsureSuccessStatusCode();

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var completeResponse = await client.PostAsync($"/api/care-requests/{createdId}/complete", null);

    completeResponse.EnsureSuccessStatusCode();
    var payload = await completeResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(payload);
    Assert.Equal("Completed", payload!.Status);
    Assert.NotNull(payload.CompletedAtUtc);
  }

  [Fact]
  public async Task POST_Complete_Should_Return_BadRequest_When_Request_Is_Not_Approved()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "complete-pending-client");
    var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "complete-pending");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "Pending request");
    await AssignCareRequestAsync(createdId, nurseUserId);

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var completeResponse = await client.PostAsync($"/api/care-requests/{createdId}/complete", null);

    Assert.Equal(HttpStatusCode.BadRequest, completeResponse.StatusCode);
  }

  [Fact]
  public async Task POST_Complete_Should_Return_Forbidden_For_Admin()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "complete-admin-client");
    var (_, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "complete-admin-nurse");
    var createdId = await CreateCareRequestAsClientAsync(clientToken, "Admin cannot complete");
    await AssignCareRequestAsync(createdId, nurseUserId);

    var adminClient = CreateAdminClient();
    var approveResponse = await adminClient.PostAsync($"/api/care-requests/{createdId}/approve", null);
    approveResponse.EnsureSuccessStatusCode();

    var completeResponse = await adminClient.PostAsync($"/api/care-requests/{createdId}/complete", null);

    Assert.Equal(HttpStatusCode.Forbidden, completeResponse.StatusCode);
  }

  [Fact]
  public async Task POST_Complete_Should_Return_BadRequest_When_Request_Date_Is_In_The_Future()
  {
    var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "complete-future-client");
    var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "complete-future-nurse");
    var createdId = await CreateCareRequestAsClientAsync(
      clientToken,
      "Future request",
      DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"));
    await AssignCareRequestAsync(createdId, nurseUserId);

    var adminClient = CreateAdminClient();
    var approveResponse = await adminClient.PostAsync($"/api/care-requests/{createdId}/approve", null);
    approveResponse.EnsureSuccessStatusCode();

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var completeResponse = await client.PostAsync($"/api/care-requests/{createdId}/complete", null);

    Assert.Equal(HttpStatusCode.BadRequest, completeResponse.StatusCode);
  }

  private HttpClient CreateAdminClient()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));
    return client;
  }

  private async Task<Guid> CreateCareRequestAsClientAsync(
    string clientToken,
    string description,
    string? careRequestDate = null)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = description,
      careRequestType = "domicilio_24h",
      careRequestDate,
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

  private sealed class CareRequestItem
  {
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }
}

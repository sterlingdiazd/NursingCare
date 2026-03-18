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
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateWriterToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      residentId = Guid.NewGuid(),
      description = "Transition me"
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var approveResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/approve", null);

    approveResponse.EnsureSuccessStatusCode();
    var payload = await approveResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(payload);
    Assert.Equal("Approved", payload!.Status);
    Assert.NotNull(payload.ApprovedAtUtc);
  }

  [Fact]
  public async Task POST_Approve_Should_Return_Forbidden_For_Nurse()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateWriterToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      residentId = Guid.NewGuid(),
      description = "Admin only approve"
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();

    var approveResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/approve", null);

    Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
  }

  [Fact]
  public async Task POST_Reject_Should_Transition_Request_To_Rejected_For_Admin()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateWriterToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      residentId = Guid.NewGuid(),
      description = "Reject me"
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var rejectResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/reject", null);

    rejectResponse.EnsureSuccessStatusCode();
    var payload = await rejectResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(payload);
    Assert.Equal("Rejected", payload!.Status);
    Assert.NotNull(payload.RejectedAtUtc);
  }

  [Fact]
  public async Task POST_Complete_Should_Transition_Approved_Request_To_Completed()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateWriterToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      residentId = Guid.NewGuid(),
      description = "Complete me"
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));
    var approveResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/approve", null);
    approveResponse.EnsureSuccessStatusCode();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateWriterToken(_factory.Services));
    var completeResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/complete", null);

    completeResponse.EnsureSuccessStatusCode();
    var payload = await completeResponse.Content.ReadFromJsonAsync<CareRequestItem>();

    Assert.NotNull(payload);
    Assert.Equal("Completed", payload!.Status);
    Assert.NotNull(payload.CompletedAtUtc);
  }

  [Fact]
  public async Task POST_Complete_Should_Return_BadRequest_When_Request_Is_Not_Approved()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateWriterToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      residentId = Guid.NewGuid(),
      description = "Pending request"
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();

    var completeResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/complete", null);

    Assert.Equal(HttpStatusCode.BadRequest, completeResponse.StatusCode);
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private sealed class CareRequestItem
  {
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
  }
}

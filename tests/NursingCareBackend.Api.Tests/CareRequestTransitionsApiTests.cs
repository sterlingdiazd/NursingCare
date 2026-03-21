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
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = "Transition me",
      careRequestType = "domicilio_24h",
      unit = 1
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
    var nurseToken = await CreateCompletedNurseTokenAsync("approve-forbidden");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = "Admin only approve",
      careRequestType = "domicilio_24h",
      unit = 1
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var approveResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/approve", null);

    Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
  }

  [Fact]
  public async Task POST_Reject_Should_Transition_Request_To_Rejected_For_Admin()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = "Reject me",
      careRequestType = "domicilio_24h",
      unit = 1
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
    var nurseToken = await CreateCompletedNurseTokenAsync("complete-success");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = "Complete me",
      careRequestType = "domicilio_24h",
      unit = 1
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
      nurseToken);
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
    var nurseToken = await CreateCompletedNurseTokenAsync("complete-pending");
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
    {
      careRequestDescription = "Pending request",
      careRequestType = "domicilio_24h",
      unit = 1
    });

    createResponse.EnsureSuccessStatusCode();
    var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
    var completeResponse = await client.PostAsync($"/api/care-requests/{created!.Id}/complete", null);

    Assert.Equal(HttpStatusCode.BadRequest, completeResponse.StatusCode);
  }

  private sealed class CreateResponse
  {
    public Guid Id { get; set; }
  }

  private async Task<string> CreateCompletedNurseTokenAsync(string scenario)
  {
    var email = $"{scenario}-{Guid.NewGuid():N}@nursingcare.local";
    var client = _factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Martinez",
      identificationNumber = "001-3344556-7",
      phone = "8095550103",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Home Care",
      bankName = "Banco Central",
      profileType = 1
    });

    registerResponse.EnsureSuccessStatusCode();
    var registered = await registerResponse.Content.ReadFromJsonAsync<RegisteredUserResponse>();
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
        identificationNumber = "001-3344556-7",
        phone = "8095550103",
        email,
        hireDate = "2026-03-21",
        specialty = "Home Care",
        licenseId = "LIC-01",
        bankName = "Banco Central",
        accountNumber = "12345",
        category = "Senior"
      });

    completeResponse.EnsureSuccessStatusCode();

    client.DefaultRequestHeaders.Authorization = null;
    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoggedInUserResponse>();
    Assert.NotNull(loginPayload);
    return loginPayload!.Token;
  }

  private sealed class RegisteredUserResponse
  {
    public Guid UserId { get; set; }
  }

  private sealed class LoggedInUserResponse
  {
    public string Token { get; set; } = string.Empty;
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

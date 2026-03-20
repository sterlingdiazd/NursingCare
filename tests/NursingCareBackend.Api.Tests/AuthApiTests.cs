using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NursingCareBackend.Api.Tests;

public sealed class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AuthApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task POST_Register_Should_Return_Token_And_Default_User_Role()
  {
    var client = _factory.CreateClient();
    var email = $"register-{Guid.NewGuid():N}@nursingcare.local";

    var response = await client.PostAsJsonAsync("/api/auth/register", new
    {
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
    Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    Assert.NotNull(payload.ExpiresAtUtc);
    Assert.Equal(email, payload.Email);
    Assert.Contains("User", payload.Roles);
  }

  [Fact]
  public async Task POST_Login_Should_Return_Token_For_Registered_User()
  {
    var client = _factory.CreateClient();
    var email = $"login-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
    Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    Assert.NotNull(payload.ExpiresAtUtc);
    Assert.Equal(email, payload.Email);
  }

  [Fact]
  public async Task POST_Login_Should_Return_BadRequest_For_Inactive_Nurse()
  {
    var client = _factory.CreateClient();
    var email = $"pending-nurse-{Guid.NewGuid():N}@nursingcare.local";

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      profileType = 1
    });

    registerResponse.EnsureSuccessStatusCode();

    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task POST_Login_Should_Return_Unauthorized_For_Invalid_Credentials()
  {
    var client = _factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email = "missing.user@nursingcare.local",
      password = "WrongPass123!"
    });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(problem);
    Assert.Equal("Invalid email or password.", problem!.Detail);
  }

  [Fact]
  public async Task GET_GoogleStart_Should_Redirect_To_Google()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/start");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);
    Assert.StartsWith(
      "https://accounts.google.com/o/oauth2/v2/auth",
      response.Headers.Location!.ToString(),
      StringComparison.Ordinal);
  }

  [Fact]
  public async Task GET_GoogleStart_Should_Include_Mobile_Target_In_State()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/start?target=mobile");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);
    Assert.Contains("state=mobile", response.Headers.Location!.ToString(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task GET_GoogleCallback_Should_Redirect_Back_To_Login_With_Tokens()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/callback?code=google-success");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var location = response.Headers.Location!.ToString();
    Assert.StartsWith("http://localhost:3000/login#", location, StringComparison.Ordinal);

    var parameters = QueryHelpers.ParseQuery("?" + response.Headers.Location.Fragment.TrimStart('#'));
    Assert.Equal("success", parameters["oauth"].ToString());
    Assert.Equal("google-success@example.com", parameters["email"].ToString());
    Assert.Equal("User", parameters["roles"].ToString());
    Assert.False(string.IsNullOrWhiteSpace(parameters["token"].ToString()));
    Assert.False(string.IsNullOrWhiteSpace(parameters["refreshToken"].ToString()));
  }

  [Fact]
  public async Task GET_GoogleCallback_Should_Redirect_Back_To_Login_With_Error_When_Google_Fails()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/callback?error=access_denied");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var location = response.Headers.Location!.ToString();
    Assert.StartsWith("http://localhost:3000/login#", location, StringComparison.Ordinal);

    var parameters = QueryHelpers.ParseQuery("?" + response.Headers.Location.Fragment.TrimStart('#'));
    Assert.Equal("error", parameters["oauth"].ToString());
  }

  [Fact]
  public async Task GET_GoogleCallback_Should_Redirect_Back_To_Mobile_Deep_Link()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/callback?code=google-success&state=mobile");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var location = response.Headers.Location!.ToString();
    Assert.StartsWith("nursingcaremobile://login", location, StringComparison.Ordinal);

    var parameters = QueryHelpers.ParseQuery(response.Headers.Location.Query);
    Assert.Equal("success", parameters["oauth"].ToString());
    Assert.Equal("google-success@example.com", parameters["email"].ToString());
    Assert.Equal("User", parameters["roles"].ToString());
  }

  [Fact]
  public async Task GET_CareRequests_Should_Return_Unauthorized_When_Token_Is_Missing()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/care-requests");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task POST_AssignRole_Should_Return_Forbidden_For_Non_Admin_User()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateToken(_factory.Services, "Nurse"));

    var response = await client.PostAsJsonAsync("/api/auth/assign-role", new
    {
      userId = Guid.NewGuid().ToString(),
      roleName = "Admin"
    });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task POST_Refresh_Should_Return_New_Tokens_For_Valid_RefreshToken()
  {
    var client = _factory.CreateClient();
    var email = $"refresh-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

    var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = loginPayload!.RefreshToken
    });

    refreshResponse.EnsureSuccessStatusCode();

    var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(refreshPayload);
    Assert.False(string.IsNullOrWhiteSpace(refreshPayload!.Token));
    Assert.False(string.IsNullOrWhiteSpace(refreshPayload.RefreshToken));
    Assert.NotEqual(loginPayload.RefreshToken, refreshPayload.RefreshToken);
  }

  [Fact]
  public async Task POST_Refresh_Should_Return_Unauthorized_For_Invalid_RefreshToken()
  {
    var client = _factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = "invalid-refresh-token"
    });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task POST_Refresh_Should_Return_Unauthorized_For_Reused_RefreshToken()
  {
    var client = _factory.CreateClient();
    var email = $"refresh-reuse-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

    var firstRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = loginPayload!.RefreshToken
    });

    firstRefreshResponse.EnsureSuccessStatusCode();

    var reusedRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = loginPayload.RefreshToken
    });

    Assert.Equal(HttpStatusCode.Unauthorized, reusedRefreshResponse.StatusCode);
  }

  private sealed class AuthResponseDto
  {
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
  }

  private sealed class ProblemDetailsDto
  {
    public string? Detail { get; set; }
  }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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
    Assert.Equal(email, payload.Email);
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

  private sealed class AuthResponseDto
  {
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
  }

  private sealed class ProblemDetailsDto
  {
    public string? Detail { get; set; }
  }
}

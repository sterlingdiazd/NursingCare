using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NursingCareBackend.Api.Tests;

public sealed class ClientProfileApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public ClientProfileApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GET_ClientProfile_Returns_Authenticated_Client_Profile()
  {
    var (token, userId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(
      _factory,
      $"client-profile-get-{Guid.NewGuid():N}");
    var client = CreateClient(token);

    var response = await client.GetAsync("/api/client/profile");

    response.EnsureSuccessStatusCode();
    var profile = await response.Content.ReadFromJsonAsync<ClientProfileDto>();
    Assert.NotNull(profile);
    Assert.Equal(userId, profile!.UserId);
    Assert.Equal("Carla", profile.Name);
    Assert.Equal("Jimenez", profile.LastName);
    Assert.Matches(@"^\d{11}$", profile.IdentificationNumber!);
    Assert.Matches(@"^\d{10}$", profile.Phone!);
  }

  [Fact]
  public async Task PUT_ClientProfile_Updates_Allowed_Identity_Fields()
  {
    var (token, userId) = await CareRequestApiAuthHelper.CreateClientTokenAsync(
      _factory,
      $"client-profile-put-{Guid.NewGuid():N}");
    var client = CreateClient(token);

    var response = await client.PutAsJsonAsync("/api/client/profile", new
    {
      name = "Maria",
      lastName = "Santos",
      identificationNumber = "00199887766",
      phone = "8295551212",
      email = "ignored@nursingcare.local",
      isActive = false
    });

    response.EnsureSuccessStatusCode();
    var profile = await response.Content.ReadFromJsonAsync<ClientProfileDto>();
    Assert.NotNull(profile);
    Assert.Equal(userId, profile!.UserId);
    Assert.Equal("Maria", profile.Name);
    Assert.Equal("Santos", profile.LastName);
    Assert.Equal("Maria Santos", profile.DisplayName);
    Assert.Equal("00199887766", profile.IdentificationNumber);
    Assert.Equal("8295551212", profile.Phone);
    Assert.True(profile.IsActive);
    Assert.NotEqual("ignored@nursingcare.local", profile.Email);
  }

  [Fact]
  public async Task PUT_ClientProfile_Returns_BadRequest_For_Invalid_Identity_Data()
  {
    var (token, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(
      _factory,
      $"client-profile-invalid-{Guid.NewGuid():N}");
    var client = CreateClient(token);

    var response = await client.PutAsJsonAsync("/api/client/profile", new
    {
      name = "Maria2",
      lastName = "Santos",
      identificationNumber = "123",
      phone = "abc"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task ClientProfile_Rejects_Admin_And_Anonymous_Users()
  {
    var anonymous = _factory.CreateClient();
    var anonymousResponse = await anonymous.GetAsync("/api/client/profile");
    Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

    var admin = CreateClient(JwtTestTokens.CreateAdminToken(_factory.Services));
    var adminResponse = await admin.GetAsync("/api/client/profile");
    Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
  }

  private HttpClient CreateClient(string token)
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
  }

  private sealed class ClientProfileDto
  {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? IdentificationNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
  }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NursingCareBackend.Api.Tests;

internal static class CareRequestApiAuthHelper
{
  public static async Task<(string Token, Guid UserId)> CreateClientTokenAsync(
    CustomWebApplicationFactory factory,
    string scenario)
  {
    var email = $"{scenario}-{Guid.NewGuid():N}@nursingcare.local";
    var client = factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Carla",
      lastName = "Jimenez",
      identificationNumber = "00122334456",
      phone = "8095550101",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      profileType = 0
    });

    registerResponse.EnsureSuccessStatusCode();
    var registered = await registerResponse.Content.ReadFromJsonAsync<AuthPayload>();
    if (registered is null)
    {
      throw new InvalidOperationException("Client registration response was empty.");
    }

    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthPayload>();
    if (loginPayload is null)
    {
      throw new InvalidOperationException("Client login response was empty.");
    }

    return (loginPayload.Token, registered.UserId);
  }

  public static async Task<(string Token, Guid UserId)> CreateCompletedNurseTokenAsync(
    CustomWebApplicationFactory factory,
    string scenario)
  {
    var email = $"{scenario}-{Guid.NewGuid():N}@nursingcare.local";
    var client = factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Martinez",
      identificationNumber = "00133445567",
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
    var registered = await registerResponse.Content.ReadFromJsonAsync<AuthPayload>();
    if (registered is null)
    {
      throw new InvalidOperationException("Nurse registration response was empty.");
    }

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(factory.Services));

    var completeResponse = await client.PutAsJsonAsync(
      $"/api/admin/nurse-profiles/{registered.UserId}/complete",
      new
      {
        name = "Luisa",
        lastName = "Martinez",
        identificationNumber = "00133445567",
        phone = "8095550103",
        email,
        hireDate = "2026-03-21",
        specialty = "Home Care",
        licenseId = "01",
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
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthPayload>();
    if (loginPayload is null)
    {
      throw new InvalidOperationException("Nurse login response was empty.");
    }

    return (loginPayload.Token, registered.UserId);
  }

  internal sealed class AuthPayload
  {
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
  }
}

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
    var identificationNumber = UniqueDigits("001", 11);
    var phone = UniqueDigits("809", 10);
    var client = factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Carla",
      lastName = "Jimenez",
      identificationNumber,
      phone,
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      profileType = 2
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
    var identificationNumber = UniqueDigits("001", 11);
    var phone = UniqueDigits("809", 10);
    var client = factory.CreateClient();

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Martinez",
      identificationNumber,
      phone,
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Atencion domiciliaria",
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
        identificationNumber,
        phone,
        email,
        hireDate = "2026-03-21",
        specialty = "Atencion domiciliaria",
        licenseId = "01",
        bankName = "Banco Central",
        accountNumber = "12345",
        category = "Senior",
        visitDailyRate = 2500m,
        homeCareMonthlyRate = 50000m
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

  private static string UniqueDigits(string prefix, int length)
  {
    var digits = string.Concat(Guid.NewGuid().ToString("N").Where(char.IsDigit));
    while (digits.Length < length)
    {
      digits += string.Concat(Guid.NewGuid().ToString("N").Where(char.IsDigit));
    }

    return (prefix + digits)[..length];
  }
}

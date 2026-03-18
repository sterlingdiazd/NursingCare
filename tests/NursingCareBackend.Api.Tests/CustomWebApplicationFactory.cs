using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
  private static string GetTestConnectionString()
  {
    var connectionString = Environment.GetEnvironmentVariable("NursingCare_TestSqlConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException("Environment variable 'NursingCare_TestSqlConnection' must be set for API tests.");
    }

    return connectionString;
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Test");

    builder.ConfigureAppConfiguration((_, configBuilder) =>
    {
      configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["GoogleOAuth:ClientId"] = "test-google-client-id",
        ["GoogleOAuth:ClientSecret"] = "test-google-client-secret",
        ["GoogleOAuth:RedirectUri"] = "https://localhost/api/auth/google/callback",
        ["GoogleOAuth:FrontendRedirectUrl"] = "http://localhost:3000/login"
      });
    });

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<IGoogleOAuthClient>();
      services.AddSingleton<IGoogleOAuthClient, FakeGoogleOAuthClient>();

      var descriptor = services.Single(
        d => d.ServiceType == typeof(DbContextOptions<NursingCareDbContext>));

      services.Remove(descriptor);

      services.AddDbContext<NursingCareDbContext>(options =>
      {
        options.UseSqlServer(GetTestConnectionString());
      });

      var sp = services.BuildServiceProvider();

      using var scope = sp.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
      db.Database.EnsureDeleted();
      db.Database.Migrate();
    });
  }

  private sealed class FakeGoogleOAuthClient : IGoogleOAuthClient
  {
    public string BuildAuthorizationUrl()
      => "https://accounts.google.com/o/oauth2/v2/auth?client_id=test-google-client-id";

    public Task<GoogleOAuthUserInfo> GetUserInfoAsync(
      string authorizationCode,
      CancellationToken cancellationToken = default)
    {
      return authorizationCode switch
      {
        "google-success" => Task.FromResult(
          new GoogleOAuthUserInfo(
            "google-subject-success",
            "google-success@example.com",
            "Google Success",
            true)),
        "google-unverified" => Task.FromResult(
          new GoogleOAuthUserInfo(
            "google-subject-unverified",
            "google-unverified@example.com",
            "Google Unverified",
            false)),
        _ => throw new InvalidOperationException("OAuth error: invalid test authorization code.")
      };
    }
  }
}

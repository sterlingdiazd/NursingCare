using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;

namespace NursingCareBackend.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
  private readonly string _testConnectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();

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
        ["GoogleOAuth:FrontendRedirectUrl"] = "http://localhost:3000/login",
        ["GoogleOAuth:MobileRedirectUrl"] = "nursingcaremobile://login"
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
        options.UseSqlServer(_testConnectionString);
      });
    });
  }

  private sealed class FakeGoogleOAuthClient : IGoogleOAuthClient
  {
    public string BuildAuthorizationUrl(string? state = null)
      => state is null
        ? "https://accounts.google.com/o/oauth2/v2/auth?client_id=test-google-client-id"
        : $"https://accounts.google.com/o/oauth2/v2/auth?client_id=test-google-client-id&state={state}";

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
        "google-success-web" => Task.FromResult(
          new GoogleOAuthUserInfo(
            "google-subject-success-web",
            "google-success-web@example.com",
            "Google Success Web",
            true)),
        "google-success-mobile" => Task.FromResult(
          new GoogleOAuthUserInfo(
            "google-subject-success-mobile",
            "google-success-mobile@example.com",
            "Google Success Mobile",
            true)),
        "google-success-complete" => Task.FromResult(
          new GoogleOAuthUserInfo(
            "google-subject-success-complete",
            "google-success-complete@example.com",
            "Google Success Complete",
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

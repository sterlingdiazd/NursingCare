using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NursingCareBackend.Api.Security;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;

namespace NursingCareBackend.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
  private readonly string _testConnectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();
  private readonly MutableTimeProvider _timeProvider = new();
  private bool _databaseInitialized;
  private readonly object _lock = new();

  public MutableTimeProvider TimeProvider => _timeProvider;
  public TestEmailService EmailService => Services.GetRequiredService<TestEmailService>();

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
      // Override ResolvedConnectionString so HealthController's raw SqlConnection
      // uses the same test database as the EF DbContext.
      services.RemoveAll<ResolvedConnectionString>();
      services.AddSingleton(new ResolvedConnectionString(_testConnectionString));

      services.RemoveAll<IGoogleOAuthClient>();
      services.AddSingleton<IGoogleOAuthClient, FakeGoogleOAuthClient>();
      services.RemoveAll<TimeProvider>();
      services.AddSingleton<TimeProvider>(_timeProvider);
      services.RemoveAll<TestEmailService>();
      services.AddSingleton<TestEmailService>();
      services.RemoveAll<IEmailService>();
      services.AddSingleton<IEmailService>(serviceProvider => serviceProvider.GetRequiredService<TestEmailService>());

      var descriptor = services.Single(
        d => d.ServiceType == typeof(DbContextOptions<NursingCareDbContext>));

      services.Remove(descriptor);

      services.AddDbContext<NursingCareDbContext>(options =>
      {
        options.UseSqlServer(_testConnectionString);
      });
    });
  }

  public new HttpClient CreateClient()
  {
    EnsureDatabaseInitialized();
    ResetTransientState();
    return base.CreateClient();
  }

  public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
  {
    EnsureDatabaseInitialized();
    ResetTransientState();
    return base.CreateClient(options);
  }

  private void ResetTransientState()
  {
    using var scope = Services.CreateScope();
    if (scope.ServiceProvider.GetRequiredService<IAuthRateLimiter>() is AuthRateLimiter authRateLimiter)
    {
      authRateLimiter.Reset();
    }
  }

  public void EnsureDatabaseInitialized()
  {
    if (_databaseInitialized)
    {
      return;
    }

    lock (_lock)
    {
      if (_databaseInitialized)
      {
        return;
      }

      using var scope = Services.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
      
      dbContext.Database.Migrate();
      EnsureSystemRoles(dbContext);
      EnsureTestAdminUser(dbContext);
      EnsureTestNurseUser(dbContext);
      
      _databaseInitialized = true;
    }
  }

  private static void EnsureSystemRoles(NursingCareDbContext db)
  {
    var existingRoleNames = db.Roles
      .Select(role => role.Name)
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var missingRoles = SystemRoles.Defaults
      .Where(role => !existingRoleNames.Contains(role.Name))
      .Select(role => new Role
      {
        Id = role.Id,
        Name = role.Name
      })
      .ToArray();

    if (missingRoles.Length > 0)
    {
      db.Roles.AddRange(missingRoles);
      db.SaveChanges();
    }
  }

  private static void EnsureTestAdminUser(NursingCareDbContext db)
  {
    var adminRoleId = SystemRoles.Defaults.First(r => r.Name == SystemRoles.Admin).Id;
    var testAdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Fixed GUID for test admin
    var testAdminEmail = "test.admin@nursingcare.local";

    var existingAdmin = db.Users
      .FirstOrDefault(u => u.Id == testAdminUserId);

    if (existingAdmin != null)
    {
      return;
    }

    var testAdminUser = new User
    {
      Id = testAdminUserId,
      Email = testAdminEmail,
      PasswordHash = "test-hash-not-used-in-tests",
      ProfileType = UserProfileType.CLIENT,
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow
    };

    var testAdminClient = new Client
    {
      UserId = testAdminUser.Id
    };

    db.Users.Add(testAdminUser);
    db.Clients.Add(testAdminClient);
    
    var userRole = new UserRole
    {
      UserId = testAdminUser.Id,
      RoleId = adminRoleId
    };

    db.UserRoles.Add(userRole);
    db.SaveChanges();
  }

  private static void EnsureTestNurseUser(NursingCareDbContext db)
  {
    var nurseRoleId = SystemRoles.Defaults.First(r => r.Name == SystemRoles.Nurse).Id;
    var testNurseUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    var testNurseEmail = "test.nurse@nursingcare.local";

    if (db.Users.Any(u => u.Id == testNurseUserId))
      return;

    var testNurseUser = new User
    {
      Id = testNurseUserId,
      Email = testNurseEmail,
      PasswordHash = "test-hash-not-used-in-tests",
      ProfileType = UserProfileType.NURSE,
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow
    };

    var testNurseProfile = new Nurse
    {
      UserId = testNurseUser.Id,
      IsActive = true
    };

    db.Users.Add(testNurseUser);
    db.Nurses.Add(testNurseProfile);

    db.UserRoles.Add(new UserRole
    {
      UserId = testNurseUser.Id,
      RoleId = nurseRoleId
    });

    db.SaveChanges();
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

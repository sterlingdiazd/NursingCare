using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.SystemSettings;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Extensions
{
  public static class MigrationExtensions
  {
    /// <summary>
    /// Applies all pending EF Core migrations at application startup.
    /// NOTE: Database must have valid data that satisfies all constraints.
    /// </summary>
    /// <param name="app">WebApplication instance</param>
    public static void ApplyMigrations(this WebApplication app)
    {
      using var scope = app.Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
      var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

      try
      {
        if (string.Equals(
          db.Database.ProviderName,
          "Microsoft.EntityFrameworkCore.InMemory",
          StringComparison.Ordinal))
        {
          db.Database.EnsureCreated();
          EnsureSystemRoles(db);
          logger.LogInformation("In-memory database created successfully.");
          return;
        }

        logger.LogInformation("Applying database migrations...");
        if (!db.Database.CanConnect())
        {
          logger.LogInformation("Database does not exist, creating...");
          db.Database.Migrate();
        }
        else
        {
          logger.LogInformation("Database exists, checking for pending migrations...");
          var pendingMigrations = db.Database.GetPendingMigrations();
          if (pendingMigrations.Any())
          {
            logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations...");
            db.Database.Migrate();
          }
          else
          {
            logger.LogInformation("No pending migrations found.");
          }
        }
        EnsureSystemRoles(db);
        EnsureSystemSettings(db);
        CatalogSeeding
          .EnsureSeededAsync(db)
          .GetAwaiter()
          .GetResult();
        logger.LogInformation("Database migrations applied successfully.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "FATAL: Failed to apply database migrations. Application cannot start.");
        throw new InvalidOperationException($"Database migration failed: {ex.Message}", ex);
      }
    }

    private static void EnsureSystemRoles(NursingCareDbContext db)
    {
      var existingRoleIds = db.Roles
        .Select(role => role.Id)
        .ToHashSet();

      var missingRoles = SystemRoles.Defaults
        .Where(role => !existingRoleIds.Contains(role.Id))
        .Select(role => new Role
        {
          Id = role.Id,
          Name = role.Name
        })
        .ToArray();

      if (missingRoles.Length == 0)
      {
        return;
      }

      db.Roles.AddRange(missingRoles);
      db.SaveChanges();
      Console.WriteLine($"Seeded {missingRoles.Length} missing system role(s).");
    }

    private static void EnsureSystemSettings(NursingCareDbContext db)
    {
      var existingKeys = db.SystemSettings
        .Select(x => x.Key)
        .ToHashSet();

      var defaultSettings = new List<SystemSetting>
      {
        new() {
          Key = "PORTAL_DEFAULT_LANGUAGE",
          Value = "es",
          Description = "Default language for the admin portal and system-generated messages.",
          Category = "Localization",
          ValueType = "Select",
          AllowedValuesJson = "[\"es\", \"en\"]",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "DASHBOARD_HIGH_SEVERITY_THRESHOLD",
          Value = "80",
          Description = "Threshold percent or count to mark dashboard metrics as high severity.",
          Category = "Dashboard",
          ValueType = "Number",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "CARE_REQUEST_AGING_THRESHOLD_HOURS",
          Value = "48",
          Description = "Number of hours after which a pending care request is considered stale.",
          Category = "Operations",
          ValueType = "Number",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "FEATURE_TOGGLE_REPORTS_V2",
          Value = "false",
          Description = "Enable or disable version 2 of the reports module.",
          Category = "General",
          ValueType = "Boolean",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "NOTIFICATIONS_POLLING_INTERVAL_MS",
          Value = "30000",
          Description = "Interval in milliseconds for the UI to poll for new admin notifications.",
          Category = "General",
          ValueType = "Number",
          ModifiedAtUtc = DateTime.UtcNow
        }
      };

      var missingSettings = defaultSettings
        .Where(x => !existingKeys.Contains(x.Key))
        .ToList();

      if (missingSettings.Count == 0)
      {
        return;
      }

      db.SystemSettings.AddRange(missingSettings);
      db.SaveChanges();
      Console.WriteLine($"Seeded {missingSettings.Count} missing system setting(s).");
    }
  }
}

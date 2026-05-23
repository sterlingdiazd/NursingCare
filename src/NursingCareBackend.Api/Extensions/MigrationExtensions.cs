using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.SystemSettings;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Extensions
{
  public static class MigrationExtensions
  {
    // SQL Server error number for "object already exists" / "column already exists"
    private const int SqlErrorColumnAlreadyExists = 2705;
    private const int SqlErrorObjectAlreadyExists = 2714;

    /// <summary>
    /// Applies all pending EF Core migrations at application startup.
    /// Fault-tolerant: logs warnings instead of crashing on schema-sync conflicts.
    /// Set SKIP_MIGRATIONS=true to bypass migration execution entirely (seeding still runs).
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

        var skipMigrations = string.Equals(
          Environment.GetEnvironmentVariable("SKIP_MIGRATIONS"),
          "true",
          StringComparison.OrdinalIgnoreCase);

        if (skipMigrations)
        {
          logger.LogWarning("SKIP_MIGRATIONS=true — skipping migration execution. Seeding will still run.");
        }
        else
        {
          ApplyPendingMigrations(db, logger);
        }

        EnsureSystemRoles(db);
        EnsureSystemSettings(db);

        try
        {
          CatalogSeeding
            .EnsureSeededAsync(db)
            .GetAwaiter()
            .GetResult();
          CareRequestSeeding
            .EnsureSeededAsync(db)
            .GetAwaiter()
            .GetResult();
          FullLifecycleSeeding
            .SeedWithContextAsync(db)
            .GetAwaiter()
            .GetResult();
        }
        catch (Exception seedEx)
        {
          logger.LogWarning(seedEx, "Seeding failed (non-fatal). Application will start with existing data.");
        }

        logger.LogInformation("Database startup sequence complete.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "FATAL: Failed to complete database startup sequence.");
        throw new InvalidOperationException($"Database startup failed: {ex.Message}", ex);
      }
    }

    private static void ApplyPendingMigrations(NursingCareDbContext db, ILogger logger)
    {
      logger.LogInformation("Applying database migrations...");

      if (!db.Database.CanConnect())
      {
        logger.LogInformation("Database does not exist — creating and applying all migrations.");
        db.Database.Migrate();
        return;
      }

      logger.LogInformation("Database exists — checking for pending migrations.");
      var pendingMigrations = db.Database.GetPendingMigrations().ToList();

      if (pendingMigrations.Count == 0)
      {
        logger.LogInformation("No pending migrations found.");
        return;
      }

      logger.LogInformation("Applying {Count} pending migration(s).", pendingMigrations.Count);

      try
      {
        db.Database.Migrate();
        logger.LogInformation("All pending migrations applied successfully.");
      }
      catch (SqlException sqlEx) when (
        sqlEx.Number == SqlErrorColumnAlreadyExists ||
        sqlEx.Number == SqlErrorObjectAlreadyExists)
      {
        logger.LogWarning(
          sqlEx,
          "Migration encountered a schema-sync conflict (SQL error {Number}: {Message}). " +
          "This typically means the database schema is already up to date despite the migration history being out of sync. " +
          "Continuing startup — set SKIP_MIGRATIONS=true to suppress this entirely.",
          sqlEx.Number,
          sqlEx.Message);
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
        },
        // Datos de empresa (aparecen en comprobantes y reportes). Editables por el dueño.
        new() {
          Key = "COMPANY_NAME",
          Value = "Sol y Luna",
          Description = "Nombre de la empresa (aparece en comprobantes y reportes).",
          Category = "Empresa",
          ValueType = "Text",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "COMPANY_RNC",
          Value = "",
          Description = "RNC de la empresa (identificación fiscal en comprobantes y reportes).",
          Category = "Empresa",
          ValueType = "Text",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "COMPANY_PHONE",
          Value = "",
          Description = "Teléfono de la empresa (aparece en comprobantes y reportes).",
          Category = "Empresa",
          ValueType = "Text",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "COMPANY_ADDRESS",
          Value = "",
          Description = "Dirección de la empresa (aparece en comprobantes y reportes).",
          Category = "Empresa",
          ValueType = "Text",
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

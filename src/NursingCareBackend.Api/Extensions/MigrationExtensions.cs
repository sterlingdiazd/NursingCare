using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.AdminPortal.Payroll;
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
    /// Set RESEED=true (Development only) to wipe the database before migration+seeding — useful
    /// for a comprehensive seed refresh. RESEED is silently ignored outside Development.
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

        // RESEED: wipe and reseed the database from scratch (Development only).
        var reseedRequested = string.Equals(
          Environment.GetEnvironmentVariable("RESEED"),
          "true",
          StringComparison.OrdinalIgnoreCase);

        if (reseedRequested)
        {
          if (app.Environment.IsDevelopment())
          {
            logger.LogWarning(
              "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            logger.LogWarning(
              "RESEED=true — DELETING the entire database to apply a fresh seed.");
            logger.LogWarning(
              "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            db.Database.EnsureDeleted();
            logger.LogWarning("Database deleted. Recreating via migrations...");
          }
          else
          {
            logger.LogError(
              "RESEED=true was set but the environment is not Development ({Environment}). " +
              "The RESEED flag is ignored outside Development to prevent accidental data loss. " +
              "No data was deleted.",
              app.Environment.EnvironmentName);
          }
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
        EnsurePayrollScheduleCompliance(db);
        EnsureCatalogDisplayNames(db);

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

    // Idempotent backfill for catalog display names. Catalogs only seed when the
    // table is empty, so existing DBs keep older copy — correct it here on startup.
    private static void EnsureCatalogDisplayNames(NursingCareDbContext db)
    {
      var medicos = db.CareRequestCategoryCatalogs.FirstOrDefault(c => c.Code == "medicos");
      if (medicos is not null && medicos.DisplayName != "Médicos")
      {
        medicos.DisplayName = "Médicos";
        db.SaveChanges();
      }

      // Correct accented service-type display names. The catalog only seeds when empty, so
      // existing databases keep an older un-accented copy — fix it idempotently on startup.
      var typeNameFixes = new Dictionary<string, string>
      {
        ["domicilio_dia_12h"] = "Domicilio día 12h",
        ["hogar_basico"] = "Hogar básico",
        ["hogar_estandar"] = "Hogar estándar",
        ["sonda_nasogastrica"] = "Sonda nasogástrica",
      };

      var typeChanged = false;
      foreach (var fix in typeNameFixes)
      {
        var row = db.CareRequestTypeCatalogs.FirstOrDefault(t => t.Code == fix.Key);
        if (row is not null && row.DisplayName != fix.Value)
        {
          row.DisplayName = fix.Value;
          typeChanged = true;
        }
      }

      if (typeChanged)
      {
        db.SaveChanges();
      }
    }

    // Idempotent backfill that enforces the payroll payment-date policy on EVERY existing period
    // (the policy previously only lived in the mobile create-period prefill). For each stored
    // PayrollPeriod we recompute (cutoff, payment) from the configured policy and write the
    // correction only when the stored values differ. Uses PayrollPeriod.CorrectSchedule so even
    // CLOSED periods (e.g. the seeded March period) are fixed — this is a data correction of the
    // derived schedule dates, not a business edit of the lines. Runs right after the settings are
    // seeded so the policy keys are present.
    private static void EnsurePayrollScheduleCompliance(NursingCareDbContext db)
    {
        var config = ReadPayrollScheduleConfig(db);

        var periods = db.PayrollPeriods.ToList();
        var changed = false;

        foreach (var period in periods)
        {
            var (cutoff, payment) = PayrollScheduleCalculator.Compute(period.StartDate, period.EndDate, config);
            if (period.CutoffDate == cutoff && period.PaymentDate == payment)
            {
                continue;
            }

            period.CorrectSchedule(cutoff, payment);
            changed = true;
        }

        if (changed)
        {
            db.SaveChanges();
            Console.WriteLine("Corrected payroll period schedule date(s) to match the payment-date policy.");
        }
    }

    // Reads the four payroll payment-date policy settings from the DB, applying defaults for any
    // missing / blank / non-numeric value. Kept in lockstep with PayrollSchedulePolicy's reader.
    private static PayrollScheduleConfig ReadPayrollScheduleConfig(NursingCareDbContext db)
    {
        var keys = new[]
        {
            "PAYROLL_PAYMENT_DATE_MODE",
            "PAYROLL_FIRST_HALF_PAYMENT_DAY",
            "PAYROLL_SECOND_HALF_PAYMENT_DAY",
            "PAYROLL_DAYS_BEFORE_MONTH_END",
        };

        var rows = db.SystemSettings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .Select(s => new { s.Key, s.Value })
            .ToDictionary(s => s.Key, s => s.Value);

        var defaults = PayrollScheduleConfig.Default;

        var rawMode = rows.GetValueOrDefault("PAYROLL_PAYMENT_DATE_MODE")?.Trim().ToUpperInvariant();
        var mode = rawMode == "DAYS_BEFORE_MONTH_END"
            ? PayrollPaymentDateMode.DaysBeforeMonthEnd
            : PayrollPaymentDateMode.FixedDay;

        return new PayrollScheduleConfig(
            mode,
            ParseIntOr(rows.GetValueOrDefault("PAYROLL_FIRST_HALF_PAYMENT_DAY"), defaults.FirstHalfPaymentDay),
            ParseIntOr(rows.GetValueOrDefault("PAYROLL_SECOND_HALF_PAYMENT_DAY"), defaults.SecondHalfPaymentDay),
            ParseIntOr(rows.GetValueOrDefault("PAYROLL_DAYS_BEFORE_MONTH_END"), defaults.DaysBeforeMonthEnd));
    }

    private static int ParseIntOr(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return int.TryParse(value.Trim(), out var parsed) ? parsed : fallback;
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
        },
        // Política de fecha de pago de nómina (configurable por el dueño en el Menú → Configuración).
        // Defaults reproduce el comportamiento actual: 1ra quincena el día 15, 2da el último día del mes.
        new() {
          Key = "PAYROLL_PAYMENT_DATE_MODE",
          Value = "FIXED_DAY",
          Description = "Cómo se calcula la fecha de pago de nómina: día fijo por quincena, o un número de días antes del fin de mes.",
          Category = "Nómina",
          ValueType = "Select",
          AllowedValuesJson = "[\"FIXED_DAY\", \"DAYS_BEFORE_MONTH_END\"]",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "PAYROLL_FIRST_HALF_PAYMENT_DAY",
          Value = "15",
          Description = "Día del mes en que se paga la 1ra quincena (1–28).",
          Category = "Nómina",
          ValueType = "Number",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "PAYROLL_SECOND_HALF_PAYMENT_DAY",
          Value = "0",
          Description = "Día del mes en que se paga la 2da quincena. 0 = último día del mes.",
          Category = "Nómina",
          ValueType = "Number",
          ModifiedAtUtc = DateTime.UtcNow
        },
        new() {
          Key = "PAYROLL_DAYS_BEFORE_MONTH_END",
          Value = "0",
          Description = "Modo 'días antes de fin de mes': la 2da quincena se paga este número de días antes del último día del mes (0 = último día).",
          Category = "Nómina",
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

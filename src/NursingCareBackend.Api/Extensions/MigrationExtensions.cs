using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Domain.Identity;
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
        db.Database.Migrate();
        EnsureSystemRoles(db);
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

      if (missingRoles.Length == 0)
      {
        return;
      }

      db.Roles.AddRange(missingRoles);
      db.SaveChanges();
      Console.WriteLine($"Seeded {missingRoles.Length} missing system role(s).");
    }
  }
}

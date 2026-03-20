using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Extensions
{
  public static class MigrationExtensions
  {
    /// <summary>
    /// Applies all pending EF Core migrations at application startup.
    /// </summary>
    /// <param name="app">WebApplication instance</param>
    public static void ApplyMigrations(this WebApplication app)
    {
      using var scope = app.Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

      try
      {
        if (string.Equals(
          db.Database.ProviderName,
          "Microsoft.EntityFrameworkCore.InMemory",
          StringComparison.Ordinal))
        {
          db.Database.EnsureCreated();
          EnsureSystemRoles(db);
          Console.WriteLine("In-memory database created successfully.");
          return;
        }

        db.Database.Migrate();
        EnsureSystemRoles(db);
        Console.WriteLine("Database created and migrations applied successfully.");
      }
      catch (Exception ex)
      {
        Console.WriteLine("Error applying migrations:");
        Console.WriteLine(ex);
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

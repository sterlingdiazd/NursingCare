using Microsoft.EntityFrameworkCore;
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
        db.Database.Migrate();
        Console.WriteLine("Database created and migrations applied successfully.");
      }
      catch (Exception ex)
      {
        Console.WriteLine("Error applying migrations:");
        Console.WriteLine(ex);
      }
    }
  }
}

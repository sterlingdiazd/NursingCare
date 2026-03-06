using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    builder.ConfigureServices(services =>
    {
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
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
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
          options.UseSqlServer(
                  "Server=localhost,1433;Database=NursingCareDb_ApiTests;User Id=sa;Password=1202lingSter89*;TrustServerCertificate=True;");
        });

      var sp = services.BuildServiceProvider();

      using var scope = sp.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
      db.Database.EnsureDeleted();
      db.Database.Migrate();
    });
  }
}

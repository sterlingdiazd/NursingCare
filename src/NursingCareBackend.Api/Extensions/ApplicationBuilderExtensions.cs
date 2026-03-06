using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Api.Middleware;

namespace NursingCareBackend.Api.Extensions;

public static class ApplicationBuilderExtensions
{
  public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
  {
    return app.UseMiddleware<ExceptionHandlingMiddleware>();
  }

  public static void LogDatabaseConfiguration(this WebApplication app)
  {
    using var scope = app.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Startup");

    var hasOverrideConnection =
      !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));

    var rawConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    var usesPlaceholder = rawConnectionString.Contains("{SQL_PASSWORD}", StringComparison.Ordinal);
    var hasSqlPasswordEnv = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQL_PASSWORD"));

    logger.LogInformation(
      "Database connection configuration: overrideConnection={OverrideConnection}, usesPlaceholder={UsesPlaceholder}, hasSqlPasswordEnv={HasSqlPasswordEnv}",
      hasOverrideConnection,
      usesPlaceholder,
      hasSqlPasswordEnv);
  }
}


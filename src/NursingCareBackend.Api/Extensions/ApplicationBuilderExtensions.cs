using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Api.Middleware;
using NursingCareBackend.Infrastructure;

namespace NursingCareBackend.Api.Extensions;

public static class ApplicationBuilderExtensions
{
  public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
  {
    return app.UseMiddleware<CorrelationIdMiddleware>();
  }

  public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
  {
    return app.UseMiddleware<ExceptionHandlingMiddleware>();
  }

  public static string GetCorrelationId(this HttpContext context)
  {
    return CorrelationIdMiddleware.GetCorrelationId(context);
  }

  public static void LogDatabaseConfiguration(this WebApplication app)
  {
    using var scope = app.Services.CreateScope();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Startup");

    var resolved = scope.ServiceProvider.GetRequiredService<ResolvedConnectionString>();
    var hasPlaceholder =
      resolved.Value.Contains("{DB_SERVER}", StringComparison.Ordinal)
      || resolved.Value.Contains("{DB_NAME}", StringComparison.Ordinal)
      || resolved.Value.Contains("{DB_USER}", StringComparison.Ordinal)
      || resolved.Value.Contains("{DB_PASSWORD}", StringComparison.Ordinal)
      || resolved.Value.Contains("{SQL_PASSWORD}", StringComparison.Ordinal);

    logger.LogInformation(
      "Database connection ready. HasUnresolvedPlaceholder={HasPlaceholder}",
      hasPlaceholder);
  }
}

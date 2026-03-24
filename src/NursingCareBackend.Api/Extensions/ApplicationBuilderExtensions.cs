using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
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

  public static IApplicationBuilder UseDetailedErrorLogging(this IApplicationBuilder app)
  {
    return app.UseMiddleware<DetailedErrorLoggingMiddleware>();
  }

  public static WebApplication UseApiMiddleware(this WebApplication app)
  {
    app.UseForwardedHeadersForProxy();
    app.UseCorrelationId();
    app.UseStructuredRequestLogging();
    app.UseExceptionHandling();

    app.UseCors("AllowAllDev");

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseDetailedErrorLogging(); // Add after UseAuthorization to catch all error responses

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
      options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nursing Care API v1");
      options.DocumentTitle = "Nursing Care API";
      options.DisplayRequestDuration();
    });

    app.MapControllers();

    return app;
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
      || resolved.Value.Contains("{DB_PASSWORD}", StringComparison.Ordinal);

    logger.LogInformation(
      "Database connection ready. HasUnresolvedPlaceholder={HasPlaceholder}",
      hasPlaceholder);
  }

  private static IApplicationBuilder UseForwardedHeadersForProxy(this IApplicationBuilder app)
  {
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
      ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto
    };

    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();

    return app.UseForwardedHeaders(forwardedHeadersOptions);
  }
}

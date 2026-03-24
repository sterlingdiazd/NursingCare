using System.Security.Claims;
using Serilog;
using Serilog.Events;

namespace NursingCareBackend.Api.Extensions;

public static class LoggingExtensions
{
  public static WebApplicationBuilder AddStructuredLogging(this WebApplicationBuilder builder)
  {
    // Don't create file logs in Azure to avoid startup issues
    builder.Host.UseSerilog((context, services, configuration) =>
    {
      configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .WriteTo.Console();
    });

    return builder;
  }

  public static IApplicationBuilder UseStructuredRequestLogging(this IApplicationBuilder app)
  {
    return app.UseSerilogRequestLogging(options =>
    {
      options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
      options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
      {
        diagnosticContext.Set("CorrelationId", httpContext.GetCorrelationId());
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("ClientApp", httpContext.Request.Headers["X-Client-App"].ToString());
        diagnosticContext.Set("ClientPlatform", httpContext.Request.Headers["X-Client-Platform"].ToString());
        diagnosticContext.Set("TraceIdentifier", httpContext.TraceIdentifier);
        diagnosticContext.Set("Authenticated", httpContext.User.Identity?.IsAuthenticated == true);

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
          diagnosticContext.Set(
            "User",
            httpContext.User.FindFirstValue(ClaimTypes.Email)
              ?? httpContext.User.Identity?.Name
              ?? "authenticated");
          diagnosticContext.Set(
            "UserEmail",
            httpContext.User.FindFirstValue(ClaimTypes.Email) ?? "unknown");
          diagnosticContext.Set(
            "UserId",
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown");
          diagnosticContext.Set(
            "Roles",
            string.Join(",", httpContext.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)));
        }
      };
    });
  }

  private static string ResolveLogsDirectory(IWebHostEnvironment environment)
  {
    var homeDirectory = Environment.GetEnvironmentVariable("HOME");
    if (!string.IsNullOrWhiteSpace(homeDirectory))
    {
      return Path.Combine(homeDirectory, "LogFiles", "Application");
    }

    return Path.Combine(environment.ContentRootPath, "Logs");
  }

  private static bool EnsureDirectoryExists(string path)
  {
    try
    {
      Directory.CreateDirectory(path);
      return true;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      Log.Warning(ex, "File logging disabled because the log directory could not be created: {LogsDirectory}", path);
      return false;
    }
  }
}

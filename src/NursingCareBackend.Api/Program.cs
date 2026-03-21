using NursingCareBackend.Api;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Middleware;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;
using NursingCareBackend.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
var logsDirectory = ResolveLogsDirectory(builder.Environment);
var fileLoggingEnabled = EnsureDirectoryExists(logsDirectory);

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

  if (fileLoggingEnabled)
  {
    configuration.WriteTo.File(
      Path.Combine(logsDirectory, "backend-.log"),
      rollingInterval: RollingInterval.Day,
      retainedFileCountLimit: 14,
      shared: true,
      outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({CorrelationId}) {Message:lj} {Properties:j}{NewLine}{Exception}");
  }
});

// CORS
builder.Services.AddCorsPolicy(builder.Configuration);

// Controllers
builder.Services.AddControllers();


// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Authentication & Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);

// Use cases
builder.Services.AddScoped<AssignCareRequestNurseHandler>();
builder.Services.AddScoped<CreateCareRequestHandler>();
builder.Services.AddScoped<TransitionCareRequestHandler>();
builder.Services.AddScoped<GetCareRequestsHandler>();
builder.Services.AddScoped<GetCareRequestByIdHandler>();

var app = builder.Build();

app.LogDatabaseConfiguration();

// --- Begin automatic migration ---
app.ApplyMigrations();
// --- End automatic migration ---

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.XForwardedFor
    | ForwardedHeaders.XForwardedHost
    | ForwardedHeaders.XForwardedProto
};

// Trust the local reverse proxy in development containers.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseCorrelationId();
app.UseSerilogRequestLogging(options =>
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
app.UseExceptionHandling();

// CORS
app.UseCors("AllowAllDev");
// app.UseCors("AllowWebApp");
// app.UseCors("AllowMobileApp");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
  options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nursing Care API v1");
  options.DocumentTitle = "Nursing Care API";
  options.DisplayRequestDuration();
});

app.MapControllers();

app.Run();

static string ResolveLogsDirectory(IWebHostEnvironment environment)
{
  var homeDirectory = Environment.GetEnvironmentVariable("HOME");
  if (!string.IsNullOrWhiteSpace(homeDirectory))
  {
    return Path.Combine(homeDirectory, "LogFiles", "Application");
  }

  return Path.Combine(environment.ContentRootPath, "Logs");
}

static bool EnsureDirectoryExists(string path)
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

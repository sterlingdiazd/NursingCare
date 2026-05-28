using NursingCareBackend.Api;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Security;
using NursingCareBackend.Application;
using NursingCareBackend.Infrastructure;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.AddStructuredLogging();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IAuthRateLimiter, AuthRateLimiter>();

// CORS
builder.Services.AddCorsPolicy(builder.Configuration);

// Controllers
builder.Services.AddApiControllers();


// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Authentication & Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);

// Application services
builder.Services.AddApplicationServices();

// Expose the per-request correlation id (set by CorrelationIdMiddleware) to inner layers so the
// audit trail carries it alongside the logs/response.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<NursingCareBackend.Application.Abstractions.ICorrelationContext,
    NursingCareBackend.Api.Infrastructure.HttpCorrelationContext>();

var app = builder.Build();

app.LogDatabaseConfiguration();

// Ops safety net: the background workers (push dispatch, payment reminders, daily overdue summary)
// only register when BackgroundWorkers:Disabled is false (see Infrastructure DI). If the flag is set,
// those run silently never — warn loudly at startup so a misconfigured demo/env is obvious.
if (builder.Configuration.GetValue<bool>("BackgroundWorkers:Disabled"))
{
    app.Logger.LogWarning(
        "Background workers are DISABLED (BackgroundWorkers:Disabled=true): push notifications, " +
        "payment reminders, and the daily overdue summary will NOT run.");
}

// --- Begin automatic migration ---
app.ApplyMigrations();
// --- End automatic migration ---

app.UseApiMiddleware();

app.Run();

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

var app = builder.Build();

app.LogDatabaseConfiguration();

// --- Begin automatic migration ---
app.ApplyMigrations();
// --- End automatic migration ---

app.UseApiMiddleware();

app.Run();

using NursingCareBackend.Api;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application;
using NursingCareBackend.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddStructuredLogging();

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

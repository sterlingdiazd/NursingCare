using NursingCareBackend.Api;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;
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

app.UseApiMiddleware();

app.Run();

using NursingCareBackend.Api;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Middleware;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;
using NursingCareBackend.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCorsPolicy(builder.Configuration);

// Controllers
builder.Services.AddControllers();
   // .AddApplicationPart(typeof(NursingCareBackend.Api.Controllers.HealthController).Assembly);


// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Authentication & Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);

// Use cases
builder.Services.AddScoped<CreateCareRequestHandler>();
builder.Services.AddScoped<GetCareRequestsHandler>();
builder.Services.AddScoped<GetCareRequestByIdHandler>();

var app = builder.Build();

app.LogDatabaseConfiguration();

// --- Begin automatic migration ---
app.ApplyMigrations();
// --- End automatic migration ---

app.UseExceptionHandling();

// CORS
app.UseCors("AllowAllDev");
// app.UseCors("AllowWebApp");
// app.UseCors("AllowMobileApp");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

using NursingCareBackend.Infrastructure;
using NursingCareBackend.Api;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;

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

// Use cases
builder.Services.AddScoped<CreateCareRequestHandler>();

var app = builder.Build();

// --- Begin automatic migration ---
app.ApplyMigrations();
// --- End automatic migration ---

// CORS
app.UseCors("AllowAllDev");
// app.UseCors("AllowWebApp");
// app.UseCors("AllowMobileApp");


app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

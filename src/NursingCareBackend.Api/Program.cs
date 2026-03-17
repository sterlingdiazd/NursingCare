using NursingCareBackend.Api;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Middleware;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;
using NursingCareBackend.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

// Enable static files for custom Swagger UI
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
  options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nursing Care API v1");
  
  // Inject custom JavaScript for JWT Authorize button
  options.HeadContent = @"
    <script src='/swagger-ui-custom.js'></script>
  ";
});

app.MapControllers();

app.Run();

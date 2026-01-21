using NursingCare.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NursingCare.Application.CareRequests.Commands.CreateCareRequest;
using NursingCare.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddInfrastructure(builder.Configuration);
// Use cases
builder.Services.AddScoped<CreateCareRequestHandler>();


// Database (SQL Server via Docker)
builder.Services.AddDbContext<NursingCareDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

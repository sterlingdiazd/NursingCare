using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;



namespace NursingCareBackend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        var sqlPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD");

        if (!string.IsNullOrEmpty(sqlPassword) &&
            connectionString.Contains("{SQL_PASSWORD}", StringComparison.Ordinal))
        {
            connectionString = connectionString.Replace("{SQL_PASSWORD}", sqlPassword, StringComparison.Ordinal);
        }

        services.AddDbContext<NursingCareDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ICareRequestRepository, CareRequestRepository>();

        return services;
    }
}

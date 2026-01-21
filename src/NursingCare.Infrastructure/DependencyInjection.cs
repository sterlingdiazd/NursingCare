using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingCare.Application.CareRequests.Commands.CreateCareRequest;
using NursingCare.Infrastructure.CareRequests;
using NursingCare.Infrastructure.Persistence;



namespace NursingCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NursingCareDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ICareRequestRepository, CareRequestRepository>();

        return services;
    }
}

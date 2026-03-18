using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Infrastructure.Authentication;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection")!;
        var resolvedConnectionString = ConnectionStringResolver.Resolve(rawConnectionString);

        services.AddSingleton(new ResolvedConnectionString(resolvedConnectionString));

        services.AddDbContext<NursingCareDbContext>(options =>
            options.UseSqlServer(resolvedConnectionString));

        // Care Request Repository
        services.AddScoped<ICareRequestRepository, CareRequestRepository>();

        // Identity Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Authentication Services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenGenerator, TokenGenerator>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Clients;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Application.AdminPortal.Users;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Application.AdminPortal.Catalog;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Infrastructure.AdminPortal;
using NursingCareBackend.Infrastructure.Catalogs;
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

        services.Configure<GoogleOAuthOptions>(options =>
        {
            var section = configuration.GetSection(GoogleOAuthOptions.SectionName);
            options.ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                ?? section["ClientId"]
                ?? string.Empty;
            options.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
                ?? section["ClientSecret"]
                ?? string.Empty;
            options.RedirectUri = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_REDIRECT_URI")
                ?? section["RedirectUri"]
                ?? string.Empty;
            options.FrontendRedirectUrl = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_FRONTEND_REDIRECT_URL")
                ?? section["FrontendRedirectUrl"]
                ?? string.Empty;
            options.MobileRedirectUrl = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_MOBILE_REDIRECT_URL")
                ?? section["MobileRedirectUrl"]
                ?? string.Empty;
        });

        services.Configure<AdminBootstrapOptions>(options =>
        {
            var section = configuration.GetSection(AdminBootstrapOptions.SectionName);
            if (bool.TryParse(section["AllowInProduction"], out var allowInProduction))
            {
                options.AllowInProduction = allowInProduction;
            }
        });

        // Care Request Repository
        services.AddScoped<ICareRequestRepository, CareRequestRepository>();
        services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
        services.AddScoped<IAdminActionQueueRepository, AdminActionQueueRepository>();
        services.AddScoped<IAdminCareRequestRepository, AdminCareRequestRepository>();
        services.AddScoped<IAdminClientManagementRepository, AdminClientManagementRepository>();
        services.AddScoped<IAdminUserManagementRepository, AdminUserManagementRepository>();
        services.AddScoped<IAdminAuditService, AdminAuditService>();

        // Identity Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Authentication Services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenGenerator, TokenGenerator>();
        services.AddScoped<IGoogleOAuthClient>(serviceProvider =>
            new GoogleOAuthClient(
                new HttpClient(),
                serviceProvider.GetRequiredService<IOptions<GoogleOAuthOptions>>()));
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAdminBootstrapPolicy, AdminBootstrapPolicy>();
        services.AddScoped<INurseProfileAdministrationService, NurseProfileAdministrationService>();

        services.AddScoped<ICareRequestPricingCalculator, CareRequestPricingCalculator>();
        services.AddScoped<IPricingPreviewService>(sp => sp.GetRequiredService<CareRequestPricingCalculator>());
        services.AddScoped<INurseCatalogService, NurseCatalogService>();
        services.AddScoped<ICatalogOptionsService, CatalogOptionsService>();
        services.AddScoped<IAdminCatalogManagementService, AdminCatalogManagementService>();

        return services;
    }
}

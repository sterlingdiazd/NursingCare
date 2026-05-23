using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Clients;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Application.AdminPortal.Shifts;
using NursingCareBackend.Application.AdminPortal.Users;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Application.AdminPortal.Reports;
using NursingCareBackend.Application.AdminPortal.Catalog;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Infrastructure.Notifications;
using NursingCareBackend.Application.AdminPortal.Settings;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Infrastructure.AdminPortal;
using NursingCareBackend.Infrastructure.Catalogs;
using NursingCareBackend.Infrastructure.Authentication;
using NursingCareBackend.Infrastructure.Email;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Identity;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Infrastructure.Payroll;
using NursingCareBackend.Application.Payroll;

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
        {
            options.UseSqlServer(resolvedConnectionString);
            options.ConfigureWarnings(w =>
                w.Log(RelationalEventId.PendingModelChangesWarning));
        });

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

        services.Configure<EmailOptions>(options =>
        {
            var section = configuration.GetSection(EmailOptions.SectionName);
            options.ConnectionString = Environment.GetEnvironmentVariable("ACS_EMAIL_CONNECTION_STRING")
                ?? section["ConnectionString"]
                ?? string.Empty;
            options.SenderAddress = Environment.GetEnvironmentVariable("ACS_EMAIL_SENDER_ADDRESS")
                ?? section["SenderAddress"]
                ?? string.Empty;
            options.SenderDisplayName = Environment.GetEnvironmentVariable("ACS_EMAIL_SENDER_DISPLAY_NAME")
                ?? section["SenderDisplayName"]
                ?? "NursingCare";
        });

        // Care Request Repository and billing
        services.AddScoped<ICareRequestRepository, CareRequestRepository>();
        services.AddScoped<IPaymentValidationRepository, PaymentValidationRepository>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IPaymentProofRepository, PaymentProofRepository>();
        services.AddScoped<IReceiptPdfService, ReceiptPdfService>();
        services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
        services.AddScoped<NursingCareBackend.Application.AdminPortal.Finance.IAdminFinanceRepository, AdminFinanceRepository>();
        services.AddScoped<IAdminActionQueueRepository, AdminActionQueueRepository>();
        services.AddScoped<IAdminCareRequestRepository, AdminCareRequestRepository>();
        services.AddScoped<IShiftRecordAdminRepository, ShiftRecordAdminRepository>();
        services.AddScoped<IAdminClientManagementRepository, AdminClientManagementRepository>();
        services.AddScoped<IAdminUserManagementRepository, AdminUserManagementRepository>();
        services.AddScoped<IAdminAuditService, AdminAuditService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        services.AddScoped<IAdminNotificationPublisher, AdminNotificationPublisher>();
        services.AddScoped<IAdminEmailNotifier, AdminEmailNotifier>();

        // Push delivery: HttpClient + worker. The worker is a long-running
        // BackgroundService that drains NotificationOutbox; the HttpClient
        // talks to the Expo Push Service. See PushDispatcherWorker.cs.
        services.AddHttpClient<IExpoPushClient, ExpoPushClient>(client =>
        {
          client.BaseAddress = new Uri("https://exp.host/--/api/v2/push/");
          client.DefaultRequestHeaders.Add("accept", "application/json");
          client.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate");
          client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IPushTokenService, PushTokenService>();
        services.AddHostedService<PushDispatcherWorker>();
        services.AddScoped<IAdminReportsRepository, AdminReportsRepository>();
        services.AddScoped<AdminPayrollRepository>();
        services.AddScoped<IAdminPayrollRepository>(sp => sp.GetRequiredService<AdminPayrollRepository>());
        services.AddScoped<INursePayrollRepository>(sp => sp.GetRequiredService<AdminPayrollRepository>());
        services.AddScoped<IAdminCompensationRulesRepository, AdminCompensationRulesRepository>();
        services.AddScoped<IAdminScheduledDeductionRepository, AdminScheduledDeductionRepository>();
        services.AddScoped<IAdminShiftRepository, AdminShiftRepository>();
        services.AddScoped<GetAdminReportHandler>();

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
        services.AddScoped<IEmailService, AcsEmailService>();
        services.AddScoped<IAdminBootstrapPolicy, AdminBootstrapPolicy>();
        services.AddScoped<INurseProfileAdministrationService, NurseProfileAdministrationService>();

        services.AddScoped<CareRequestPricingCalculator>();
        services.AddScoped<ICareRequestPricingCalculator>(sp => sp.GetRequiredService<CareRequestPricingCalculator>());
        services.AddScoped<IPricingPreviewService>(sp => sp.GetRequiredService<CareRequestPricingCalculator>());
        services.AddScoped<INurseCatalogService, NurseCatalogService>();
        services.AddScoped<ICatalogOptionsService, CatalogOptionsService>();
        services.AddScoped<IAdminCatalogManagementService, AdminCatalogManagementService>();
        services.AddScoped<IAdminSettingsManagementService, AdminSettingsManagementService>();
        services.AddScoped<IPayrollCompensationService, PayrollCompensationService>();
        services.AddScoped<IPayrollRecalculationService, PayrollRecalculationService>();
        services.AddScoped<IAdminPayrollOverrideRepository, AdminPayrollOverrideRepository>();
        services.AddScoped<IPayrollVoucherService, PayrollVoucherService>();
        services.AddScoped<IPayrollReportExportService, PayrollReportExportService>();
        services.AddScoped<NursingCareBackend.Application.AdminPortal.Payroll.ICompanyInfoProvider, NursingCareBackend.Infrastructure.Payroll.CompanyInfoProvider>();
        services.AddScoped<IScheduledDeductionService, ScheduledDeductionService>();

        services.Configure<CompanyInfoOptions>(options =>
        {
            var section = configuration.GetSection(CompanyInfoOptions.SectionName);
            options.Name = section["Name"] ?? "NursingCare";
            options.Rnc = section["Rnc"];
        });

        services.Configure<NursingCareBackend.Infrastructure.Fiscal.FiscalOptions>(
            configuration.GetSection(NursingCareBackend.Infrastructure.Fiscal.FiscalOptions.SectionName));
        services.AddScoped<NursingCareBackend.Application.CareRequests.IInvoiceNumberGenerator,
            NursingCareBackend.Infrastructure.CareRequests.InvoiceNumberGenerator>();

        return services;
    }
}

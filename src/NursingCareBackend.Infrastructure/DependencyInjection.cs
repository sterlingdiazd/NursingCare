using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
using NursingCareBackend.Application.Communications;
using NursingCareBackend.Infrastructure.AdminPortal;
using NursingCareBackend.Infrastructure.Catalogs;
using NursingCareBackend.Infrastructure.Authentication;
using NursingCareBackend.Infrastructure.Email;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.PaymentOcr;
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

        services.Configure<EmailArchiveOptions>(
            configuration.GetSection(EmailArchiveOptions.SectionName));
        services.Configure<PaymentOcrOptions>(options =>
        {
            var section = configuration.GetSection(PaymentOcrOptions.SectionName);
            options.Provider = Environment.GetEnvironmentVariable("PAYMENT_OCR_PROVIDER")
                ?? section["Provider"]
                ?? "Auto";
            options.AzureVisionEndpoint = Environment.GetEnvironmentVariable("PAYMENT_OCR_AZURE_VISION_ENDPOINT")
                ?? section["AzureVisionEndpoint"]
                ?? string.Empty;
            options.AzureVisionKey = Environment.GetEnvironmentVariable("PAYMENT_OCR_AZURE_VISION_KEY")
                ?? section["AzureVisionKey"]
                ?? string.Empty;
            options.GoogleVisionEndpoint = Environment.GetEnvironmentVariable("PAYMENT_OCR_GOOGLE_VISION_ENDPOINT")
                ?? section["GoogleVisionEndpoint"]
                ?? "https://vision.googleapis.com/v1/images:annotate";
            options.GoogleVisionApiKey = Environment.GetEnvironmentVariable("PAYMENT_OCR_GOOGLE_VISION_KEY")
                ?? section["GoogleVisionApiKey"]
                ?? string.Empty;
            options.OcrSpaceEndpoint = Environment.GetEnvironmentVariable("PAYMENT_OCR_OCRSPACE_ENDPOINT")
                ?? section["OcrSpaceEndpoint"]
                ?? "https://api.ocr.space/parse/image";
            options.OcrSpaceApiKey = Environment.GetEnvironmentVariable("PAYMENT_OCR_OCRSPACE_KEY")
                ?? section["OcrSpaceApiKey"]
                ?? string.Empty;
            options.TimeoutSeconds =
                int.TryParse(
                    Environment.GetEnvironmentVariable("PAYMENT_OCR_TIMEOUT_SECONDS") ?? section["TimeoutSeconds"],
                    out var timeout) && timeout > 0
                    ? timeout
                    : 8;
        });

        // DEMO communications redirect: when enabled, every outgoing email AND every wa.me
        // WhatsApp link is redirected to a single configured demo contact (the owner) so demos
        // never message real nurses/clients. Env var with appsettings-section fallback, mirroring
        // the EmailOptions pattern above. PRODUCTION-safe: Enabled is OFF unless explicitly
        // turned on (env DEMO_COMMS_ENABLED="true" or section "true"). Absent/unparseable => off.
        services.Configure<DemoCommunicationsOptions>(options =>
        {
            var section = configuration.GetSection(DemoCommunicationsOptions.SectionName);
            var enabledRaw = Environment.GetEnvironmentVariable("DEMO_COMMS_ENABLED")
                ?? section["Enabled"];
            options.Enabled = bool.TryParse(enabledRaw, out var enabled) && enabled;
            options.ContactEmail = Environment.GetEnvironmentVariable("DEMO_COMMS_EMAIL")
                ?? section["ContactEmail"]
                ?? string.Empty;
            options.ContactPhone = Environment.GetEnvironmentVariable("DEMO_COMMS_PHONE")
                ?? section["ContactPhone"]
                ?? string.Empty;
        });

        // Care Request Repository and billing
        services.AddScoped<ICareRequestRepository, CareRequestRepository>();
        services.AddScoped<IPaymentValidationRepository, PaymentValidationRepository>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<ICreditNoteRepository, CreditNoteRepository>();
        services.AddScoped<IPaymentProofRepository, PaymentProofRepository>();
        // Payment-proof OCR: a stacked reader behind one orchestrator -
        // Azure AI Vision (primary) -> Google Vision (high 20 MB image limit) ->
        // OCR.space (free reserve). Each provider's HttpClient is bounded by
        // PaymentOcr:TimeoutSeconds so a hung provider can never freeze the client; on
        // timeout the extractor returns null and the orchestrator falls to the next one.
        var ocrTimeoutSeconds =
            int.TryParse(
                Environment.GetEnvironmentVariable("PAYMENT_OCR_TIMEOUT_SECONDS")
                    ?? configuration.GetSection(PaymentOcrOptions.SectionName)["TimeoutSeconds"],
                out var parsedOcrTimeout) && parsedOcrTimeout > 0
                ? parsedOcrTimeout
                : 8;
        services.AddHttpClient<AzureVisionTextExtractor>(client =>
            client.Timeout = TimeSpan.FromSeconds(ocrTimeoutSeconds));
        services.AddHttpClient<GoogleVisionTextExtractor>(client =>
            client.Timeout = TimeSpan.FromSeconds(ocrTimeoutSeconds));
        services.AddHttpClient<OcrSpaceTextExtractor>(client =>
            client.Timeout = TimeSpan.FromSeconds(ocrTimeoutSeconds));
        services.AddScoped<IPaymentProofTextExtractor>(sp => sp.GetRequiredService<AzureVisionTextExtractor>());
        services.AddScoped<IPaymentProofTextExtractor>(sp => sp.GetRequiredService<GoogleVisionTextExtractor>());
        services.AddScoped<IPaymentProofTextExtractor>(sp => sp.GetRequiredService<OcrSpaceTextExtractor>());
        services.AddScoped<IPaymentProofOcrService, PaymentProofOcrService>();
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
        services.AddScoped<IUserNotificationService, UserNotificationService>();
        // DEMO redirect also gates USER notifications (push/in-app to nurses/clients): suppressed when
        // demo mode is on, so a demo never reaches a real user. Outermost decorator over the publisher.
        services.AddScoped<UserNotificationPublisher>();
        services.AddScoped<IUserNotificationPublisher>(sp => new Notifications.DemoRedirectUserNotificationPublisher(
            sp.GetRequiredService<UserNotificationPublisher>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NursingCareBackend.Application.Communications.DemoCommunicationsOptions>>(),
            sp.GetRequiredService<ILogger<Notifications.DemoRedirectUserNotificationPublisher>>()));

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
        if (!configuration.GetValue<bool>("BackgroundWorkers:Disabled"))
        {
            services.AddHostedService<PushDispatcherWorker>();
            services.AddHostedService<OverdueNotificationWorker>();
        }
        services.Configure<OverdueNotificationOptions>(
            configuration.GetSection(OverdueNotificationOptions.SectionName));
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
        // Email pipeline (decorator chain, resolved outermost-first):
        //   DemoRedirectEmailService → ArchivingEmailService → AcsEmailService.
        // 1) AcsEmailService is the real transport.
        // 2) ArchivingEmailService wraps it so every outgoing email is archived to disk (.eml).
        // 3) DemoRedirectEmailService is OUTERMOST: while the DEMO redirect is on, it rewrites the
        //    recipient/subject to the demo contact BEFORE archiving, so the archive reflects what
        //    was actually sent. Consumers resolve IEmailService and get the full chain.
        services.AddScoped<AcsEmailService>();
        services.AddScoped<ArchivingEmailService>(sp => new ArchivingEmailService(
            sp.GetRequiredService<AcsEmailService>(),
            sp.GetRequiredService<IOptions<EmailArchiveOptions>>(),
            sp.GetRequiredService<IOptions<EmailOptions>>(),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<ILogger<ArchivingEmailService>>()));
        services.AddScoped<IEmailService>(sp => new DemoRedirectEmailService(
            sp.GetRequiredService<ArchivingEmailService>(),
            sp.GetRequiredService<IOptions<DemoCommunicationsOptions>>()));
        services.AddSingleton<IEmailTemplateRenderer, Email.MarkdownEmailTemplateRenderer>();
        services.AddScoped<IAdminBootstrapPolicy, AdminBootstrapPolicy>();
        services.AddScoped<INurseProfileAdministrationService, NurseProfileAdministrationService>();

        services.AddScoped<CareRequestPricingCalculator>();
        services.AddScoped<ICareRequestPricingCalculator>(sp => sp.GetRequiredService<CareRequestPricingCalculator>());
        services.AddScoped<IPricingPreviewService>(sp => sp.GetRequiredService<CareRequestPricingCalculator>());
        services.AddScoped<INurseCatalogService, NurseCatalogService>();
        services.AddScoped<ICatalogOptionsService, CatalogOptionsService>();
        services.AddScoped<IAdminCatalogManagementService, AdminCatalogManagementService>();
        services.AddScoped<IAdminSettingsManagementService, AdminSettingsManagementService>();
        services.AddScoped<NursingCareBackend.Application.AdminPortal.Payroll.IPayrollSchedulePolicy,
            NursingCareBackend.Infrastructure.Payroll.PayrollSchedulePolicy>();
        services.AddScoped<IPayrollCompensationService, PayrollCompensationService>();
        services.AddScoped<IPayrollRecalculationService, PayrollRecalculationService>();
        services.AddScoped<IAdminPayrollOverrideRepository, AdminPayrollOverrideRepository>();
        services.AddScoped<IPayrollVoucherService, PayrollVoucherService>();
        services.AddScoped<NursingCareBackend.Application.AdminPortal.Payroll.Validation.IFinancialOutputValidator,
            NursingCareBackend.Infrastructure.Payroll.Validation.FinancialOutputValidator>();
        services.AddScoped<INursePeriodPaymentRepository, NursePeriodPaymentRepository>();
        services.AddScoped<IPayrollReportExportService, PayrollReportExportService>();
        services.AddScoped<NursingCareBackend.Application.AdminPortal.Payroll.ICompanyInfoProvider, NursingCareBackend.Infrastructure.Payroll.CompanyInfoProvider>();
        services.AddScoped<IScheduledDeductionService, ScheduledDeductionService>();
        services.AddScoped<NursingCareBackend.Application.AdminPortal.Payroll.IPayrollSchedulePolicy, NursingCareBackend.Infrastructure.Payroll.PayrollSchedulePolicy>();

        services.Configure<CompanyInfoOptions>(options =>
        {
            var section = configuration.GetSection(CompanyInfoOptions.SectionName);
            options.Name = section["Name"] ?? "NursingCare";
            options.Rnc = section["Rnc"];
        });

        services.Configure<NursingCareBackend.Infrastructure.Fiscal.FiscalOptions>(
            configuration.GetSection(NursingCareBackend.Infrastructure.Fiscal.FiscalOptions.SectionName));
        // Fiscal/invoicing config is read live from the owner-editable SystemSettings (FISCAL_*),
        // falling back to appsettings FiscalOptions. Mirrors CompanyInfoProvider so edits apply
        // without a redeploy. Consumed by InvoiceNumberGenerator and ReceiptPdfService.
        services.AddScoped<NursingCareBackend.Application.CareRequests.IFiscalSettingsProvider,
            NursingCareBackend.Infrastructure.Fiscal.FiscalSettingsProvider>();
        services.AddScoped<NursingCareBackend.Application.CareRequests.IInvoiceNumberGenerator,
            NursingCareBackend.Infrastructure.CareRequests.InvoiceNumberGenerator>();

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Infrastructure.Email;
using NursingCareBackend.Infrastructure.Notifications;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Unit tests for <see cref="OverdueNotificationWorker.RunOnceAsync"/>.
/// The two RunOnce tests exercise template rendering, notification publishing,
/// and email composition with real SQL Server state.
/// The template-only test is fully in-memory.
/// </summary>
public sealed class OverdueNotificationWorkerTests : IDisposable
{
    private readonly List<string> _createdConnectionStrings = new();

    // -----------------------------------------------------------------------
    // Fakes
    // -----------------------------------------------------------------------

    private sealed class FakeAdminEmailNotifier : IAdminEmailNotifier
    {
        public List<(string Subject, string HtmlBody)> Sent { get; } = [];

        public Task SendToAdminsAsync(string subject, string htmlBody, CancellationToken cancellationToken)
        {
            Sent.Add((subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private NursingCareDbContext CreateSqlServerDbContext()
    {
        var connectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();
        _createdConnectionStrings.Add(connectionString);
        var options = new DbContextOptionsBuilder<NursingCareDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var db = new NursingCareDbContext(options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        return db;
    }

    private static ServiceProvider BuildServiceProvider(
        NursingCareDbContext db,
        FakeAdminNotificationPublisher publisher,
        FakeAdminEmailNotifier emailNotifier)
    {
        var services = new ServiceCollection();
        services.AddScoped<NursingCareDbContext>(_ => db);
        services.AddSingleton<IAdminNotificationPublisher>(publisher);
        services.AddSingleton<IAdminEmailNotifier>(emailNotifier);
        services.AddSingleton<IEmailTemplateRenderer, MarkdownEmailTemplateRenderer>();
        return services.BuildServiceProvider();
    }

    private static OverdueNotificationWorker CreateWorker(IServiceProvider services)
    {
        var options = Options.Create(new OverdueNotificationOptions { RunHourUtc = 8 });
        return new OverdueNotificationWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OverdueNotificationWorker>.Instance,
            options);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunOnceAsync_Should_Render_Template_And_Send_Email_When_Pending_Requests_Exist()
    {
        // Arrange — real SQL Server, seeded with one pending care request via raw SQL.
        await using var db = CreateSqlServerDbContext();

        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO CareRequests
                (Id, UserID, Description, CareRequestType, UnitType, Unit, Price, Total,
                 Status, CreatedAtUtc, UpdatedAtUtc, PricingCategoryCode,
                 CategoryFactorSnapshot, DistanceFactorMultiplierSnapshot,
                 ComplexityMultiplierSnapshot, VolumeDiscountPercentSnapshot)
            VALUES
                ({0}, {1}, N'Servicio domicilio 24h', N'domicilio_24h', N'Dia', 1, 4200.00, 4200.00,
                 0, GETUTCDATE(), GETUTCDATE(), N'domicilio', 1.2, 1.0, 1.0, 0)",
            requestId, userId);

        var publisher = new FakeAdminNotificationPublisher();
        var emailNotifier = new FakeAdminEmailNotifier();
        var services = BuildServiceProvider(db, publisher, emailNotifier);
        var worker = CreateWorker(services);

        // Act
        await worker.RunOnceAsync(DateTime.UtcNow, CancellationToken.None);

        // Assert — in-app notification published.
        Assert.Single(publisher.PublishedRequests,
            r => r.Category == "daily_admin_summary");

        // Assert — email sent with rendered HTML.
        Assert.Single(emailNotifier.Sent);
        var (subject, htmlBody) = emailNotifier.Sent[0];
        Assert.Contains("Resumen diario", subject);
        Assert.Contains("<h1 ", htmlBody); // Markdig renders # heading as <h1 id="...">
        Assert.Contains("Resumen General", htmlBody);
    }

    [Fact]
    public async Task RunOnceAsync_Should_Skip_Notifications_When_No_Pending_Requests()
    {
        // Arrange — empty SQL Server database.
        await using var db = CreateSqlServerDbContext();

        var publisher = new FakeAdminNotificationPublisher();
        var emailNotifier = new FakeAdminEmailNotifier();
        var services = BuildServiceProvider(db, publisher, emailNotifier);
        var worker = CreateWorker(services);

        // Act
        await worker.RunOnceAsync(DateTime.UtcNow, CancellationToken.None);

        // Assert — no notifications or emails sent.
        Assert.Empty(publisher.PublishedRequests);
        Assert.Empty(emailNotifier.Sent);
    }

    [Fact]
    public void TemplateRenderer_Should_Substitute_All_Placeholders()
    {
        // Arrange — fully in-memory, no SQL Server needed.
        var renderer = new MarkdownEmailTemplateRenderer();
        var vars = new Dictionary<string, string>
        {
            { "{{Fecha}}", "24/05/2026" },
            { "{{PendientesCount}}", "3" },
            { "{{VencidasCount}}", "1" },
            { "{{SinAsignarCount}}", "2" },
            { "{{MontoEnJuego}}", "12,600.00" },
            { "{{SolicitudesRows}}", "| Servicio 1 | 24/05/2026 | 4,200.00 | Pending | Maria Lopez | Aprobar o rechazar |" },
            { "{{CtaDeepLink}}", "/admin/care-requests?view=overdue" },
        };

        // Act
        var html = renderer.Render("daily-admin-summary", vars);

        // Assert
        Assert.Contains("24/05/2026", html);
        Assert.Contains("12,600.00", html);
        Assert.Contains("/admin/care-requests?view=overdue", html);
        Assert.DoesNotContain("{{", html); // no unresolved placeholders
    }

    // -----------------------------------------------------------------------
    // Teardown
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        foreach (var connectionString in _createdConnectionStrings)
        {
            try
            {
                var options = new DbContextOptionsBuilder<NursingCareDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;
                using var db = new NursingCareDbContext(options);
                db.Database.EnsureDeleted();
            }
            catch { /* best-effort teardown; never fail the run */ }
        }
    }
}

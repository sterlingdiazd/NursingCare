using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Infrastructure.Email;
using NursingCareBackend.Infrastructure.Notifications;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// T2.2 — OverdueNotificationWorker.ProcessPaymentRemindersAsync. Real SQL Server state, fake
/// publishers. Covers: due reminder after the window (not before), overdue reminder next day,
/// idempotency (flags prevent re-send), and that a Paid request is never reminded.
/// </summary>
public sealed class PaymentReminderWorkerTests
{
    private const int Invoiced = 5;
    private const int Paid = 6;
    private const int PaymentReported = 8;

    [Fact]
    public async Task DueReminder_SentToClientAndAdmin_AfterWindow_AndStamped()
    {
        await using var db = CreateSqlServerDbContext();
        var (id, userId) = await SeedOwedAsync(db, Invoiced, completedHoursAgo: 3); // > 2h due window

        var (user, admin, worker) = Build(db);
        await worker.ProcessPaymentRemindersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Single(user.Published, n => n.RecipientUserId == userId && n.Category == "payment_due");
        Assert.Single(admin.PublishedRequests, n => n.Category == "payment_due");

        var stamped = await db.CareRequests.AsNoTracking().FirstAsync(r => r.Id == id);
        Assert.NotNull(stamped.PaymentDueReminderSentAtUtc);
        Assert.Null(stamped.PaymentOverdueReminderSentAtUtc);
    }

    [Fact]
    public async Task DueReminder_NotSent_BeforeWindow()
    {
        await using var db = CreateSqlServerDbContext();
        var (id, _) = await SeedOwedAsync(db, Invoiced, completedHoursAgo: 1); // < 2h

        var (user, admin, worker) = Build(db);
        await worker.ProcessPaymentRemindersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Empty(user.Published);
        Assert.Empty(admin.PublishedRequests);
        var r = await db.CareRequests.AsNoTracking().FirstAsync(x => x.Id == id);
        Assert.Null(r.PaymentDueReminderSentAtUtc);
    }

    [Fact]
    public async Task OverdueReminder_SentNextDay_AndStampsBothFlags()
    {
        await using var db = CreateSqlServerDbContext();
        var (id, userId) = await SeedOwedAsync(db, Invoiced, completedHoursAgo: 25); // > 24h overdue

        var (user, admin, worker) = Build(db);
        await worker.ProcessPaymentRemindersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Single(user.Published, n => n.RecipientUserId == userId && n.Category == "payment_overdue");
        Assert.Single(admin.PublishedRequests, n => n.Category == "payment_overdue");

        var r = await db.CareRequests.AsNoTracking().FirstAsync(x => x.Id == id);
        Assert.NotNull(r.PaymentOverdueReminderSentAtUtc);
        Assert.NotNull(r.PaymentDueReminderSentAtUtc); // due is also marked so no stale due nudge follows
    }

    [Fact]
    public async Task Reminders_AreIdempotent_AcrossTicks()
    {
        await using var db = CreateSqlServerDbContext();
        await SeedOwedAsync(db, Invoiced, completedHoursAgo: 3);

        var (user, admin, worker) = Build(db);
        await worker.ProcessPaymentRemindersAsync(DateTime.UtcNow, CancellationToken.None);
        await worker.ProcessPaymentRemindersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Single(user.Published);  // not re-sent on the second tick
        Assert.Single(admin.PublishedRequests);
    }

    [Fact]
    public async Task PaidRequest_IsNeverReminded()
    {
        await using var db = CreateSqlServerDbContext();
        await SeedOwedAsync(db, Paid, completedHoursAgo: 25);

        var (user, admin, worker) = Build(db);
        await worker.ProcessPaymentRemindersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Empty(user.Published);
        Assert.Empty(admin.PublishedRequests);
    }

    [Fact]
    public async Task PaymentReported_IsNotReminded()
    {
        // Client already reported (awaiting admin verification) — do not nag them to pay.
        await using var db = CreateSqlServerDbContext();
        await SeedOwedAsync(db, PaymentReported, completedHoursAgo: 25);

        var (user, admin, worker) = Build(db);
        await worker.ProcessPaymentRemindersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Empty(user.Published);
        Assert.Empty(admin.PublishedRequests);
    }

    [Fact]
    public async Task DailySummary_Counts_OverduePayments()
    {
        await using var db = CreateSqlServerDbContext();
        await SeedOwedAsync(db, Invoiced, completedHoursAgo: 25); // overdue payment

        var (_, admin, worker) = Build(db);
        await worker.RunOnceAsync(DateTime.UtcNow, CancellationToken.None);

        var summary = Assert.Single(admin.PublishedRequests, n => n.Category == "daily_admin_summary");
        Assert.Contains("pago(s) vencido(s)", summary.Body);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static NursingCareDbContext CreateSqlServerDbContext()
    {
        var connectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();
        var options = new DbContextOptionsBuilder<NursingCareDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var db = new NursingCareDbContext(options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<(Guid Id, Guid UserId)> SeedOwedAsync(
        NursingCareDbContext db, int status, int completedHoursAgo)
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow.AddHours(-completedHoursAgo);
        // PaidAtUtc is left NULL — the worker filters on Status (Paid is excluded by status), so the
        // paid-date value is irrelevant to these tests.
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO CareRequests
                (Id, UserID, Description, CareRequestType, UnitType, Unit, Price, Total,
                 Status, CreatedAtUtc, UpdatedAtUtc, CompletedAtUtc, InvoicedAtUtc, InvoiceNumber,
                 PricingCategoryCode, CategoryFactorSnapshot,
                 DistanceFactorMultiplierSnapshot, ComplexityMultiplierSnapshot, VolumeDiscountPercentSnapshot)
            VALUES
                ({0}, {1}, N'Servicio domicilio', N'domicilio_24h', N'Dia', 1, 4200.00, 4200.00,
                 {2}, GETUTCDATE(), GETUTCDATE(), {3}, {3}, N'FAC-T22',
                 N'domicilio', 1.2, 1.0, 1.0, 0)",
            id, userId, status, completedAt);
        return (id, userId);
    }

    private static (FakeUserNotificationPublisher User, FakeAdminNotificationPublisher Admin, OverdueNotificationWorker Worker) Build(
        NursingCareDbContext db)
    {
        var user = new FakeUserNotificationPublisher();
        var admin = new FakeAdminNotificationPublisher();
        var services = new ServiceCollection();
        // Singleton (not scoped): the worker wraps work in a DI scope and disposes it; a scoped
        // registration would dispose this shared context, breaking the test's post-call assertions.
        // The test owns disposal via `await using`.
        services.AddSingleton<NursingCareDbContext>(db);
        services.AddSingleton<IAdminNotificationPublisher>(admin);
        services.AddSingleton<IUserNotificationPublisher>(user);
        services.AddSingleton<IAdminEmailNotifier, NoopAdminEmailNotifier>();
        services.AddSingleton<IEmailTemplateRenderer, MarkdownEmailTemplateRenderer>();
        var sp = services.BuildServiceProvider();

        var worker = new OverdueNotificationWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OverdueNotificationWorker>.Instance,
            Options.Create(new OverdueNotificationOptions()));
        return (user, admin, worker);
    }

    private sealed class FakeUserNotificationPublisher : IUserNotificationPublisher
    {
        public List<UserNotificationPublishRequest> Published { get; } = [];
        public Task PublishToUserAsync(UserNotificationPublishRequest request, CancellationToken ct = default)
        {
            Published.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class NoopAdminEmailNotifier : IAdminEmailNotifier
    {
        public Task SendToAdminsAsync(string subject, string htmlBody, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

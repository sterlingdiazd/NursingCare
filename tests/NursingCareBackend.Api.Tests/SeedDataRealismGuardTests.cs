using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Realism guard: fails CI if the seeders regenerate data that reads as fake/placeholder to a
/// demo viewer (test surnames, "Bank Test", *.test emails, "lifecycle seed" markers, raw English
/// copy, placeholder audit IDs) or if the demo-critical finance numbers would be empty on a fresh
/// seed. This is the durable enforcement of "seed data must look real" — symptom fixes to the live
/// DB do not satisfy it; the seeder source must.
/// </summary>
public sealed class SeedDataRealismGuardTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    // Case-insensitive substrings that betray seed/placeholder data in user-facing text.
    private static readonly string[] SeedyTokens =
    {
        "lifecycle", "seed", "placeholder", "lorem", "dummy", "mock",
        "care service for", "care request requiring",
        "@nurses.test", "@ejemplo.com", "@test.com",
    };

    public SeedDataRealismGuardTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseInitialized();
    }

    // The test harness (CustomWebApplicationFactory) injects a fixture nurse on a .local domain
    // that is NOT seeder output; scope the realism guard to production-seeded users.
    private const string FixtureEmailDomain = "@nursingcare.local";

    [Fact]
    public async Task Nurse_Identity_Is_Realistic()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var seededUsers = db.Users.AsNoTracking().Where(u => !u.Email.EndsWith(FixtureEmailDomain));

        var placeholderSurnames = await seededUsers
            .Where(u => u.LastName == "Nurse" || u.LastName == "Client")
            .Select(u => u.DisplayName).ToListAsync();
        Assert.True(placeholderSurnames.Count == 0,
            $"Users with placeholder surnames: {string.Join(", ", placeholderSurnames)}");

        // No production nurse may carry "Bank Test"; ACTIVE nurses must have a real bank. Pending
        // (IsActive=false) profiles legitimately have no banking yet, so null is allowed there.
        var badBanks = await (from n in db.Nurses.AsNoTracking()
                              join u in db.Users.AsNoTracking() on n.UserId equals u.Id
                              where !u.Email.EndsWith(FixtureEmailDomain)
                                 && (n.BankName == "Bank Test" || ((n.BankName == null || n.BankName == "") && n.IsActive))
                              select u.DisplayName).ToListAsync();
        Assert.True(badBanks.Count == 0,
            $"Active nurses with placeholder/empty bank: {string.Join(", ", badBanks)}");

        var testEmails = await seededUsers
            .Where(u => u.Email.EndsWith(".test") || u.Email.EndsWith("@ejemplo.com") || u.Email.EndsWith("@test.com"))
            .Select(u => u.Email).ToListAsync();
        Assert.True(testEmails.Count == 0, $"Users with seed-domain emails: {string.Join(", ", testEmails)}");

        var staleDisplayNames = await seededUsers
            .Where(u => u.DisplayName != (u.Name + " " + u.LastName))
            .Select(u => u.DisplayName).ToListAsync();
        Assert.True(staleDisplayNames.Count == 0,
            $"Users with DisplayName != 'Name LastName': {string.Join(", ", staleDisplayNames)}");
    }

    [Fact]
    public async Task UserFacing_Text_Has_No_Seed_Markers()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var texts = new List<string?>();
        texts.AddRange(await db.PayrollLines.AsNoTracking().Select(p => p.Description).ToListAsync());
        texts.AddRange(await db.ServiceExecutions.AsNoTracking().Select(e => e.Notes).ToListAsync());
        texts.AddRange(await db.CareRequests.AsNoTracking().Select(c => c.Description).ToListAsync());
        texts.AddRange(await db.CareRequests.AsNoTracking().Select(c => c.CareRequestReason).ToListAsync());
        texts.AddRange(await db.Users.AsNoTracking().Select(u => u.DisplayName).ToListAsync());

        var offenders = texts
            .Where(t => t is not null && SeedyTokens.Any(tok => t.Contains(tok, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Seed-y text found ({offenders.Count}): {string.Join(" | ", offenders.Take(15))}");
    }

    [Fact]
    public async Task PayrollLine_Descriptions_Do_Not_Expose_Raw_Service_Codes()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        // Raw codes contain an underscore (e.g. "domicilio_24h", "hogar_diario"); human labels do not.
        var rawCoded = await db.PayrollLines.AsNoTracking()
            .Where(p => p.Description.Contains("_"))
            .Select(p => p.Description).Distinct().ToListAsync();

        Assert.True(rawCoded.Count == 0,
            $"Payroll descriptions exposing raw codes: {string.Join(" | ", rawCoded)}");
    }

    [Fact]
    public async Task AuditLog_EntityIds_Are_Not_Placeholders()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var placeholders = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId != null && (a.EntityId.StartsWith("lifecycle-") || a.EntityId == "march-2026"))
            .Select(a => a.EntityId!).ToListAsync();

        Assert.True(placeholders.Count == 0,
            $"Audit logs with placeholder EntityIds: {string.Join(", ", placeholders)}");
    }

    [Fact]
    public async Task Finance_Has_CurrentMonth_Executions_So_Dashboard_Is_Not_Empty()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var currentMonthExecutions = await db.ServiceExecutions.AsNoTracking()
            .CountAsync(e => e.ServiceDate >= monthStart && e.ServiceDate <= today);
        Assert.True(currentMonthExecutions > 0,
            "No ServiceExecutions in the current month — the finance/Ganancia dashboard would read zero on a fresh seed.");

        var currentMonthPaid = await db.CareRequests.AsNoTracking()
            .CountAsync(c => c.PaidAtUtc != null && c.PaidAtUtc >= monthStart.ToDateTime(TimeOnly.MinValue));
        Assert.True(currentMonthPaid > 0,
            "No care requests paid in the current month — finance 'Cobrado' (collected) would read zero on a fresh seed.");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingCareBackend.Domain.SystemSettings;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Fiscal;
using NursingCareBackend.Infrastructure.Payroll;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Locks the live-editability of the fiscal/invoicing config and the payroll cutoff offset:
/// values are read fresh from the owner-editable SystemSettings (FISCAL_* / PAYROLL_*) per call,
/// with a fallback to the appsettings <see cref="FiscalOptions"/> defaults when a key is absent or
/// blank. Runs against a real SQL Server database so the SystemSettings reads behave exactly as in
/// production (mirrors CompanyInfoProvider). No restart/redeploy is needed for an edit to take hold.
/// </summary>
public sealed class FiscalSettingsLiveEditTests : IDisposable
{
    private readonly List<string> _createdConnectionStrings = new();

    private NursingCareDbContext CreateDbContext()
    {
        var connectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();
        _createdConnectionStrings.Add(connectionString);
        var options = new DbContextOptionsBuilder<NursingCareDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new NursingCareDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    // The appsettings fallback. Left at FiscalOptions defaults — exactly the seeded defaults.
    private static FiscalSettingsProvider Provider(NursingCareDbContext db, FiscalOptions? fallback = null)
        => new(db, Options.Create(fallback ?? new FiscalOptions()));

    private static async Task SetSettingAsync(NursingCareDbContext db, string key, string value, string valueType)
    {
        var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                Description = $"test {key}",
                Category = "Facturación",
                ValueType = valueType,
                ModifiedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Value = value;
        }

        await db.SaveChangesAsync();
        // Drop the change tracker so the provider's AsNoTracking read sees committed state only.
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Provider_Returns_Appsettings_Fallback_When_Setting_Absent()
    {
        await using var db = CreateDbContext();
        Assert.Empty(await db.SystemSettings.Where(s => s.Key.StartsWith("FISCAL_")).ToListAsync());

        var fallback = new FiscalOptions
        {
            Rnc = "130-99999-1",
            ItbisRatePercent = 18m,
            NcfEnabled = true,
            NcfType = "E31",
            InvoiceNumberPrefix = "ACME",
            CurrencyCode = "USD",
            LegalFooter = "Pie de prueba",
        };

        var fiscal = await Provider(db, fallback).GetAsync(CancellationToken.None);

        Assert.Equal("130-99999-1", fiscal.Rnc);
        Assert.Equal(18m, fiscal.ItbisRatePercent);
        Assert.True(fiscal.NcfEnabled);
        Assert.Equal("E31", fiscal.NcfType);
        Assert.Equal("ACME", fiscal.InvoiceNumberPrefix);
        Assert.Equal("USD", fiscal.CurrencyCode);
        Assert.Equal("Pie de prueba", fiscal.LegalFooter);
    }

    [Fact]
    public async Task Provider_Returns_Db_Value_When_Setting_Set_And_Falls_Back_When_Blank()
    {
        await using var db = CreateDbContext();

        // Default appsettings (NcfEnabled=false, prefix=SOL, ITBIS=0).
        var before = await Provider(db).GetAsync(CancellationToken.None);
        Assert.False(before.NcfEnabled);
        Assert.Equal("SOL", before.InvoiceNumberPrefix);
        Assert.Equal(0m, before.ItbisRatePercent);

        await SetSettingAsync(db, "FISCAL_NCF_ENABLED", "true", "Boolean");
        await SetSettingAsync(db, "FISCAL_ITBIS_RATE_PERCENT", "18", "Number");
        await SetSettingAsync(db, "FISCAL_INVOICE_PREFIX", "MED", "String");
        await SetSettingAsync(db, "FISCAL_NCF_TYPE", "E31", "Select");

        var after = await Provider(db).GetAsync(CancellationToken.None);
        Assert.True(after.NcfEnabled);
        Assert.Equal(18m, after.ItbisRatePercent);
        Assert.Equal("MED", after.InvoiceNumberPrefix);
        Assert.Equal("E31", after.NcfType);

        // A blank setting falls back to the appsettings default (prefix SOL), proving blank != ""
        // override.
        await SetSettingAsync(db, "FISCAL_INVOICE_PREFIX", "   ", "String");
        var blanked = await Provider(db).GetAsync(CancellationToken.None);
        Assert.Equal("SOL", blanked.InvoiceNumberPrefix);
    }

    [Fact]
    public async Task InvoiceNumberGenerator_Sees_Fiscal_Mode_And_Prefix_Edits_Live()
    {
        await using var db = CreateDbContext();
        var generator = new InvoiceNumberGenerator(db, Provider(db));

        // Default: fiscal mode OFF, proforma uses the SOL prefix.
        Assert.False(await generator.IsFiscalModeEnabledAsync(CancellationToken.None));
        var firstNumber = await generator.NextProformaAsync(new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        Assert.StartsWith("SOL-202605-", firstNumber);

        // Owner flips NCF_ENABLED and changes the prefix at runtime — no new generator/restart.
        await SetSettingAsync(db, "FISCAL_NCF_ENABLED", "true", "Boolean");
        await SetSettingAsync(db, "FISCAL_INVOICE_PREFIX", "MED", "String");

        Assert.True(await generator.IsFiscalModeEnabledAsync(CancellationToken.None));
        var afterNumber = await generator.NextProformaAsync(new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        Assert.StartsWith("MED-202605-", afterNumber);
    }

    [Fact]
    public async Task PayrollSchedulePolicy_Reads_Cutoff_Offset_From_Setting()
    {
        await using var db = CreateDbContext();
        var policy = new PayrollSchedulePolicy(db);
        var endDate = new DateOnly(2026, 5, 31);

        // Default offset is 2 (historical behavior) when the setting is absent.
        Assert.Equal(2, await policy.GetCutoffDaysBeforeEndAsync(CancellationToken.None));
        Assert.Equal(new DateOnly(2026, 5, 29), await policy.ResolveCutoffDateAsync(endDate, CancellationToken.None));

        // Owner changes the cutoff offset to 5 days before the period end — live.
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "PAYROLL_CUTOFF_DAYS_BEFORE_END",
            Value = "5",
            Description = "test cutoff",
            Category = "Nómina",
            ValueType = "Number",
            ModifiedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(5, await policy.GetCutoffDaysBeforeEndAsync(CancellationToken.None));
        Assert.Equal(new DateOnly(2026, 5, 26), await policy.ResolveCutoffDateAsync(endDate, CancellationToken.None));
    }

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

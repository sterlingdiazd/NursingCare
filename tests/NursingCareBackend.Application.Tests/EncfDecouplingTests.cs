using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CompleteByAdmin;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.PayCareRequest;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Locks the e-NCF decoupling (fiscal comprobante is NOT burned on completion; it is issued only at
/// payment confirmation in fiscal mode). Runs against a real SQL Server database so the
/// InvoiceNumberGenerator's SQL COUNT-based sequencing — the load-bearing logic that keeps the two
/// counters independent — is exercised exactly as in production.
/// </summary>
public sealed class EncfDecouplingTests : IDisposable
{
    private static readonly Guid NurseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
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

    private static InvoiceNumberGenerator Generator(NursingCareDbContext db, bool ncfEnabled) =>
        new(db, new FakeFiscalSettingsProvider(ncfEnabled, ncfType: "E32", invoicePrefix: "SOL"));

    private static CareRequest NewApprovedRequest()
    {
        var cr = CareRequest.Create(new CareRequestCreateParams
        {
            UserID = Guid.NewGuid(),
            Description = "Servicio de prueba",
            CareRequestType = "domicilio_24h",
            UnitType = "dia_completo",
            AssignedNurse = NurseId,
            Unit = 1,
            Price = 3500m,
            Total = 4200m,
            DistanceFactor = "local",
            ComplexityLevel = "estandar",
            PricingCategoryCode = "domicilio",
            CategoryFactorSnapshot = 1.2m,
            DistanceFactorMultiplierSnapshot = 1.0m,
            ComplexityMultiplierSnapshot = 1.0m,
            VolumeDiscountPercentSnapshot = 0,
            CreatedAtUtc = DateTime.UtcNow,
        });
        cr.Approve(DateTime.UtcNow.AddMinutes(-10));
        return cr;
    }

    private static CompleteByAdminHandler CompleteHandler(NursingCareDbContext db, bool ncfEnabled) =>
        new(
            new DbCareRequestRepository(db),
            new NoopAdminNotifications(),
            new NoopUserNotifications(),
            new NoopPayroll(),
            new RecordingAudit(),
            Generator(db, ncfEnabled));

    private static PayCareRequestHandler PayHandler(NursingCareDbContext db, bool ncfEnabled, RecordingAudit audit) =>
        new(
            new DbCareRequestRepository(db),
            new DbPaymentValidationRepository(db),
            audit,
            new NoopUserNotifications(),
            Generator(db, ncfEnabled));

    // Invariant 1: NcfEnabled=false (today's default) -> complete assigns SOL- proforma; Ncf stays
    // null after pay (behavior identical to today).
    [Fact]
    public async Task NcfDisabled_Complete_Then_Pay_Leaves_Proforma_And_Null_Ncf()
    {
        using var db = CreateDbContext();
        var cr = NewApprovedRequest();
        db.CareRequests.Add(cr);
        await db.SaveChangesAsync();

        await CompleteHandler(db, ncfEnabled: false)
            .Handle(new CompleteByAdminCommand(cr.Id, Guid.NewGuid()), CancellationToken.None);

        var afterComplete = await db.CareRequests.AsNoTracking().FirstAsync(c => c.Id == cr.Id);
        Assert.Equal(CareRequestStatus.Invoiced, afterComplete.Status);
        Assert.StartsWith("SOL-", afterComplete.InvoiceNumber);
        Assert.Matches(@"^SOL-\d{6}-\d{4}$", afterComplete.InvoiceNumber!);
        Assert.Null(afterComplete.Ncf);

        await PayHandler(db, ncfEnabled: false, new RecordingAudit())
            .Handle(new PayCareRequestCommand(cr.Id, "REF-001", DateTime.UtcNow, Guid.NewGuid()), CancellationToken.None);

        var afterPay = await db.CareRequests.AsNoTracking().FirstAsync(c => c.Id == cr.Id);
        Assert.Equal(CareRequestStatus.Paid, afterPay.Status);
        Assert.StartsWith("SOL-", afterPay.InvoiceNumber);
        Assert.Null(afterPay.Ncf);
        Assert.Null(afterPay.NcfIssuedAtUtc);
    }

    // Invariant 2: NcfEnabled=true -> complete assigns SOL- proforma and Ncf is STILL NULL (no NCF
    // burned on complete); after Pay -> Ncf is exactly one E32########## set once.
    [Fact]
    public async Task NcfEnabled_Ncf_Is_Null_On_Complete_And_Issued_Once_On_Pay()
    {
        using var db = CreateDbContext();
        var cr = NewApprovedRequest();
        db.CareRequests.Add(cr);
        await db.SaveChangesAsync();

        await CompleteHandler(db, ncfEnabled: true)
            .Handle(new CompleteByAdminCommand(cr.Id, Guid.NewGuid()), CancellationToken.None);

        var afterComplete = await db.CareRequests.AsNoTracking().FirstAsync(c => c.Id == cr.Id);
        Assert.StartsWith("SOL-", afterComplete.InvoiceNumber); // proforma, never an NCF
        Assert.Null(afterComplete.Ncf);                          // NCF NOT burned on complete

        var audit = new RecordingAudit();
        await PayHandler(db, ncfEnabled: true, audit)
            .Handle(new PayCareRequestCommand(cr.Id, "REF-002", DateTime.UtcNow, Guid.NewGuid()), CancellationToken.None);

        var afterPay = await db.CareRequests.AsNoTracking().FirstAsync(c => c.Id == cr.Id);
        Assert.Equal(CareRequestStatus.Paid, afterPay.Status);
        Assert.StartsWith("SOL-", afterPay.InvoiceNumber); // proforma untouched
        Assert.NotNull(afterPay.Ncf);
        Assert.Matches(@"^E32\d{10}$", afterPay.Ncf!);     // exactly one E32##########
        Assert.NotNull(afterPay.NcfIssuedAtUtc);
        Assert.Contains(audit.Records, r => r.Action == "IssueFiscalReceipt" && r.Notes!.Contains(afterPay.Ncf!));
    }

    // Invariant 3: void/cancel BEFORE pay (NcfEnabled=true) -> no Ncf assigned (no fiscal sequence
    // burned).
    [Fact]
    public async Task NcfEnabled_Void_Before_Pay_Burns_No_Fiscal_Sequence()
    {
        using var db = CreateDbContext();
        var cr = NewApprovedRequest();
        db.CareRequests.Add(cr);
        await db.SaveChangesAsync();

        await CompleteHandler(db, ncfEnabled: true)
            .Handle(new CompleteByAdminCommand(cr.Id, Guid.NewGuid()), CancellationToken.None);

        var tracked = await db.CareRequests.FirstAsync(c => c.Id == cr.Id);
        tracked.Void("Cliente canceló antes de pagar", DateTime.UtcNow);
        await db.SaveChangesAsync();

        var afterVoid = await db.CareRequests.AsNoTracking().FirstAsync(c => c.Id == cr.Id);
        Assert.Equal(CareRequestStatus.Voided, afterVoid.Status);
        Assert.Null(afterVoid.Ncf);
        Assert.Null(afterVoid.NcfIssuedAtUtc);

        // The fiscal sequence has advanced by zero: the next NCF the generator would hand out is #1.
        var nextNcf = await Generator(db, ncfEnabled: true).NextFiscalNcfAsync(DateTime.UtcNow, CancellationToken.None);
        Assert.Equal("E320000000001", nextNcf);
    }

    // Invariant 4: proforma and NCF counters are independent. Issuing several proformas must not
    // advance the NCF sequence; issuing an NCF must not advance the proforma sequence.
    [Fact]
    public async Task Proforma_And_Ncf_Counters_Are_Independent()
    {
        using var db = CreateDbContext();

        // Complete three requests in the same month with fiscal mode ON -> three SOL proformas,
        // zero NCFs (none paid yet).
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var cr = NewApprovedRequest();
            db.CareRequests.Add(cr);
            await db.SaveChangesAsync();
            await CompleteHandler(db, ncfEnabled: true)
                .Handle(new CompleteByAdminCommand(cr.Id, Guid.NewGuid()), CancellationToken.None);
            ids.Add(cr.Id);
        }

        var proformas = await db.CareRequests.AsNoTracking()
            .Where(c => ids.Contains(c.Id)).Select(c => c.InvoiceNumber!).OrderBy(n => n).ToListAsync();
        Assert.Equal(3, proformas.Distinct().Count());           // three distinct proformas
        Assert.All(proformas, p => Assert.StartsWith("SOL-", p));

        // Despite three proformas, the NCF sequence has NOT advanced: next NCF is still #1.
        Assert.Equal("E320000000001",
            await Generator(db, ncfEnabled: true).NextFiscalNcfAsync(DateTime.UtcNow, CancellationToken.None));

        // Pay the first two -> two NCFs (#1, #2). The proforma sequence is unaffected.
        foreach (var id in ids.Take(2))
        {
            await PayHandler(db, ncfEnabled: true, new RecordingAudit())
                .Handle(new PayCareRequestCommand(id, "REF", DateTime.UtcNow, Guid.NewGuid()), CancellationToken.None);
        }

        var issuedNcfs = await db.CareRequests.AsNoTracking()
            .Where(c => c.Ncf != null).Select(c => c.Ncf!).ToListAsync();
        Assert.Equal(2, issuedNcfs.Count);
        Assert.Equal(2, issuedNcfs.Distinct().Count());          // independent, no collision
        Assert.Contains("E320000000001", issuedNcfs);
        Assert.Contains("E320000000002", issuedNcfs);

        // Next proforma is still #4 for the month (issuing NCFs did not advance it).
        var nextProforma = await Generator(db, ncfEnabled: true).NextProformaAsync(DateTime.UtcNow, CancellationToken.None);
        Assert.Matches(@"^SOL-\d{6}-0004$", nextProforma);
    }

    public void Dispose()
    {
        foreach (var connectionString in _createdConnectionStrings)
        {
            try
            {
                var options = new DbContextOptionsBuilder<NursingCareDbContext>().UseSqlServer(connectionString).Options;
                using var db = new NursingCareDbContext(options);
                db.Database.EnsureDeleted();
            }
            catch { /* best-effort teardown */ }
        }
    }

    // --- Test doubles ---------------------------------------------------------------------------

    private sealed class DbCareRequestRepository : ICareRequestRepository
    {
        private readonly NursingCareDbContext _db;
        public DbCareRequestRepository(NursingCareDbContext db) => _db = db;

        public async Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken)
        {
            _db.CareRequests.Add(careRequest);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(CareRequest careRequest, CancellationToken cancellationToken)
        {
            if (_db.Entry(careRequest).State == EntityState.Detached)
                _db.CareRequests.Update(careRequest);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CareRequest>> GetAllAsync(CareRequestAccessScope scope, CancellationToken cancellationToken)
            => await _db.CareRequests.ToListAsync(cancellationToken);

        public async Task<CareRequest?> GetByIdAsync(Guid id, CareRequestAccessScope scope, CancellationToken cancellationToken)
            => await _db.CareRequests.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        public Task<int> CountByUserAndUnitTypeAsync(Guid userID, string unitType, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class DbPaymentValidationRepository : IPaymentValidationRepository
    {
        private readonly NursingCareDbContext _db;
        public DbPaymentValidationRepository(NursingCareDbContext db) => _db = db;

        public async Task AddAsync(PaymentValidation paymentValidation, CancellationToken cancellationToken)
        {
            _db.PaymentValidations.Add(paymentValidation);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<PaymentValidation?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken)
            => await _db.PaymentValidations.FirstOrDefaultAsync(p => p.CareRequestId == careRequestId, cancellationToken);
    }

    private sealed class RecordingAudit : IAdminAuditService
    {
        public List<AdminAuditRecord> Records { get; } = new();
        public Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class NoopPayroll : IPayrollCompensationService
    {
        public Task RecordExecutionForCompletedCareRequestAsync(CareRequest careRequest, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NoopAdminNotifications : IAdminNotificationPublisher
    {
        public Task PublishToAdminsAsync(AdminNotificationPublishRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoopUserNotifications : IUserNotificationPublisher
    {
        public Task PublishToUserAsync(UserNotificationPublishRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    // Stand-in for the live SystemSettings-backed provider: this suite locks the NCF sequencing math,
    // not the settings plumbing, so a fixed FiscalSettings is sufficient.
    private sealed class FakeFiscalSettingsProvider : IFiscalSettingsProvider
    {
        private readonly FiscalSettings _settings;
        public FakeFiscalSettingsProvider(bool ncfEnabled, string ncfType, string invoicePrefix)
            => _settings = new FiscalSettings(
                Rnc: null,
                ItbisRatePercent: 0m,
                NcfEnabled: ncfEnabled,
                NcfType: ncfType,
                InvoiceNumberPrefix: invoicePrefix,
                CurrencyCode: "DOP",
                LegalFooter: null);

        public Task<FiscalSettings> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings);
    }
}

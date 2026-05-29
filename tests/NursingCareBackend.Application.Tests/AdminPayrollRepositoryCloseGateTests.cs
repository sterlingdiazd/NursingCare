using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.AdminPortal;
using NursingCareBackend.Infrastructure.Payroll;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;
using Xunit;

namespace NursingCareBackend.Application.Tests;

// Locks the P2 safe-close fix: ClosePeriodAsync re-evaluates close warnings INSIDE the close
// path (not from a stale controller preflight). When unacknowledged warnings exist at close time
// the close is blocked with PeriodCloseResult.RequiresConfirmation regardless of whether any
// preflight ran. This makes the money-safe gate authoritative and TOCTOU-safe.
// See AdminPayrollRepository.ClosePeriodAsync.
public sealed class AdminPayrollRepositoryCloseGateTests : IDisposable
{
    private static readonly Guid NurseId = Guid.Parse("33333333-3333-3333-3333-333333333333");

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
        CatalogSeeding.EnsureSeededAsync(context).GetAwaiter().GetResult();

        return context;
    }

    private static async Task SeedNurseAsync(NursingCareDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            Id = NurseId,
            Email = "nurse.p2@example.com",
            ProfileType = UserProfileType.NURSE,
            Name = "Enfermera",
            LastName = "Cierre",
            PasswordHash = "x",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            NurseProfile = new Nurse
            {
                UserId = NurseId,
                IsActive = true,
                Category = "domicilio",
                VisitDailyRate = 1000m,
                HomeCareMonthlyRate = 0m,
                HomeCareMonthlyExpectedDays = 23.83m,
            },
        });
        await dbContext.SaveChangesAsync();
    }

    // Arranges a period that (a) has activity (a deduction record, so it is not "Empty") and
    // (b) has a close warning at close time: a completed ServiceExecution whose service date is
    // inside the window with NO payroll line for it (unliquidated → would go unpaid).
    private static async Task<Guid> SeedPeriodWithUnliquidatedWarningAsync(NursingCareDbContext dbContext)
    {
        var start = new DateOnly(2099, 6, 1);
        var end = new DateOnly(2099, 6, 15);

        var period = PayrollPeriod.Create(
            startDate: start,
            endDate: end,
            cutoffDate: end,
            paymentDate: end,
            createdAtUtc: DateTime.UtcNow);
        dbContext.PayrollPeriods.Add(period);

        // Period activity so close does not short-circuit to Empty.
        dbContext.DeductionRecords.Add(DeductionRecord.Create(
            nurseUserId: NurseId,
            payrollPeriodId: period.Id,
            deductionType: DeductionType.Other,
            label: "Actividad",
            amount: 10m,
            notes: null,
            effectiveAtUtc: DateTime.UtcNow,
            createdAtUtc: DateTime.UtcNow));

        // Completed service in-window with no payroll line → unliquidated-service warning.
        var executedAtUtc = new DateTime(2099, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        dbContext.ServiceExecutions.Add(ServiceExecution.Create(
            careRequestId: Guid.NewGuid(),
            nurseUserId: NurseId,
            shiftRecordId: null,
            compensationRuleId: null,
            employmentType: CompensationEmploymentType.PerService,
            variant: ServiceExecutionVariant.Standard,
            executedAtUtc: executedAtUtc,
            careRequestType: "domicilio_24h",
            unitType: "dia_completo",
            unit: 1,
            pricingCategoryCode: "domicilio",
            distanceFactorCode: "local",
            complexityLevelCode: "estandar",
            basePrice: 3500m,
            careRequestTotal: 4200m,
            clientBasePrice: 3500m,
            categoryFactorSnapshot: 1.2m,
            distanceMultiplierSnapshot: 1.0m,
            complexityMultiplierSnapshot: 1.0m,
            volumeDiscountPercentSnapshot: 0,
            subtotalBeforeSupplies: 3500m,
            medicalSuppliesCost: 0m,
            ruleBaseCompensationPercent: 0m,
            ruleFixedAmountPerUnit: 0m,
            ruleTransportIncentivePercent: 0m,
            ruleComplexityBonusPercent: 0m,
            ruleMedicalSuppliesPercent: 0m,
            ruleVariantPercent: 0m,
            baseCompensation: 1000m,
            transportIncentive: 0m,
            complexityBonus: 0m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            manualOverrideAmount: null,
            notes: null,
            createdAtUtc: DateTime.UtcNow));

        await dbContext.SaveChangesAsync();
        return period.Id;
    }

    [Fact]
    public async Task ClosePeriod_Should_Block_When_Warnings_Exist_And_Not_Acknowledged_AtCloseTime()
    {
        // Arrange: a period with an at-close-time warning. No preflight is invoked here — this
        // proves the gate is authoritative inside the close path, not dependent on the controller.
        await using var dbContext = CreateDbContext();
        await SeedNurseAsync(dbContext);
        var periodId = await SeedPeriodWithUnliquidatedWarningAsync(dbContext);

        var repository = new AdminPayrollRepository(dbContext, new PayrollSchedulePolicy(dbContext));

        // Sanity: the warning is genuinely present.
        var warnings = await repository.GetCloseWarningsAsync(periodId, CancellationToken.None);
        Assert.True(warnings.HasWarnings);
        Assert.True(warnings.UnliquidatedServices > 0);

        // Act: close WITHOUT acknowledging.
        var result = await repository.ClosePeriodAsync(periodId, acknowledgeWarnings: false, CancellationToken.None);

        // Assert: blocked, and the period remains open (not locked).
        Assert.Equal(PeriodCloseResult.RequiresConfirmation, result);

        var period = await dbContext.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == periodId);
        Assert.False(period.IsClosed);
    }

    [Fact]
    public async Task ClosePeriod_Should_Succeed_When_Warnings_Acknowledged()
    {
        // Arrange: the same warning-bearing period.
        await using var dbContext = CreateDbContext();
        await SeedNurseAsync(dbContext);
        var periodId = await SeedPeriodWithUnliquidatedWarningAsync(dbContext);

        var repository = new AdminPayrollRepository(dbContext, new PayrollSchedulePolicy(dbContext));

        // Act: close WITH acknowledgement.
        var result = await repository.ClosePeriodAsync(periodId, acknowledgeWarnings: true, CancellationToken.None);

        // Assert: the explicit acknowledgement closes the period.
        Assert.Equal(PeriodCloseResult.Success, result);

        var period = await dbContext.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == periodId);
        Assert.True(period.IsClosed);
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

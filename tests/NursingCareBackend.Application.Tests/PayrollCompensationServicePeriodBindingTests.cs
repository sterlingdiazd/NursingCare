using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Payroll;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;
using Xunit;

namespace NursingCareBackend.Application.Tests;

// Locks the P1-3 fix: a completed care-request's payroll line must bind to whichever
// existing PayrollPeriod CONTAINS the service date (StartDate <= date <= EndDate),
// including admin-created NON-STANDARD periods, instead of always materializing a fresh
// standard quincena. See PayrollCompensationService.GetOrCreatePayrollPeriodAsync.
public sealed class PayrollCompensationServicePeriodBindingTests : IDisposable
{
  private static readonly Guid NurseId = Guid.Parse("22222222-2222-2222-2222-222222222222");

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
    // The service only hard-requires the assigned nurse User (with its Nurse profile rate
    // fields). The CareRequest itself is passed by value, so it need not be persisted.
    var nurse = new User
    {
      Id = NurseId,
      Email = "nurse.p13@example.com",
      ProfileType = UserProfileType.NURSE,
      Name = "Enfermera",
      LastName = "Prueba",
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
    };

    dbContext.Users.Add(nurse);
    await dbContext.SaveChangesAsync();
  }

  // Completed domicilio request whose service date (CompletedAtUtc) falls in March 2026.
  private static CareRequest CreateCompletedDomicilioRequest(DateTime completedAtUtc)
  {
    var careRequest = CareRequest.Create(new CareRequestCreateParams
    {
      UserID = Guid.NewGuid(),
      Description = "Servicio domicilio P1-3",
      CareRequestReason = null,
      CareRequestType = "domicilio_24h",
      UnitType = "dia_completo",
      SuggestedNurse = null,
      AssignedNurse = NurseId,
      Unit = 1,
      Price = 3500m,
      Total = 4200m,
      ClientBasePrice = null,
      DistanceFactor = "local",
      ComplexityLevel = "estandar",
      MedicalSuppliesCost = null,
      CareRequestDate = null,
      PricingCategoryCode = "domicilio",
      CategoryFactorSnapshot = 1.2m,
      DistanceFactorMultiplierSnapshot = 1.0m,
      ComplexityMultiplierSnapshot = 1.0m,
      VolumeDiscountPercentSnapshot = 0,
      LineBeforeVolumeDiscount = null,
      UnitPriceAfterVolumeDiscount = null,
      SubtotalBeforeSupplies = null,
      CreatedAtUtc = completedAtUtc.AddMinutes(-10),
    });

    careRequest.Approve(completedAtUtc.AddMinutes(-5));
    careRequest.Complete(completedAtUtc, NurseId);
    return careRequest;
  }

  [Fact]
  public async Task RecordExecution_Should_Bind_Line_To_Existing_NonStandard_Period_Containing_ServiceDate()
  {
    // Arrange: a NON-STANDARD admin period spanning the full month of March 2026
    // (2026-03-01..2026-03-31 is not a standard quincena, which would be 1..15 or 16..31).
    await using var dbContext = CreateDbContext();
    await SeedNurseAsync(dbContext);

    var customPeriod = PayrollPeriod.Create(
      startDate: new DateOnly(2026, 3, 1),
      endDate: new DateOnly(2026, 3, 31),
      cutoffDate: new DateOnly(2026, 3, 29),
      paymentDate: new DateOnly(2026, 3, 31),
      createdAtUtc: DateTime.UtcNow);
    dbContext.PayrollPeriods.Add(customPeriod);
    await dbContext.SaveChangesAsync();

    // Service date 2026-03-20 falls inside the custom month period but would otherwise
    // map to the standard second-half quincena (2026-03-16..2026-03-31).
    var completedAtUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
    var careRequest = CreateCompletedDomicilioRequest(completedAtUtc);

    var service = new PayrollCompensationService(dbContext, new PayrollSchedulePolicy(dbContext));

    // Act
    await service.RecordExecutionForCompletedCareRequestAsync(careRequest, CancellationToken.None);

    // Assert: the line is bound to the existing non-standard period...
    var line = await dbContext.PayrollLines.SingleAsync();
    Assert.Equal(customPeriod.Id, line.PayrollPeriodId);

    // ...and NO new standard quincena was created — the custom period is the only one.
    var periods = await dbContext.PayrollPeriods.ToListAsync();
    Assert.Single(periods);
    Assert.Equal(customPeriod.Id, periods[0].Id);
  }

  [Fact]
  public async Task RecordExecution_Should_Create_Standard_Quincena_When_No_Period_Covers_ServiceDate()
  {
    // Arrange: no period exists at all (CatalogSeeding does not seed PayrollPeriods).
    await using var dbContext = CreateDbContext();
    await SeedNurseAsync(dbContext);

    Assert.Empty(await dbContext.PayrollPeriods.ToListAsync());

    // Service date 2026-04-20 (second half) => standard quincena 2026-04-16..2026-04-30.
    var completedAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
    var careRequest = CreateCompletedDomicilioRequest(completedAtUtc);

    var service = new PayrollCompensationService(dbContext, new PayrollSchedulePolicy(dbContext));

    // Act
    await service.RecordExecutionForCompletedCareRequestAsync(careRequest, CancellationToken.None);

    // Assert: behavior preserved — exactly one standard quincena is created and the line binds to it.
    var period = await dbContext.PayrollPeriods.SingleAsync();
    Assert.Equal(new DateOnly(2026, 4, 16), period.StartDate);
    Assert.Equal(new DateOnly(2026, 4, 30), period.EndDate);

    var line = await dbContext.PayrollLines.SingleAsync();
    Assert.Equal(period.Id, line.PayrollPeriodId);
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

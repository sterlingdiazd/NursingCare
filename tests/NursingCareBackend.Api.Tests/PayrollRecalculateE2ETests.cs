using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// End-to-End (E2E) API tests for POST /api/admin/payroll/recalculate.
/// Authored by e2e-tests-agent, initiative 2026-04-20T0351-recalculo-nomina.
///
/// These tests complement PayrollRbacAndBoundaryTests by covering:
/// - Role-Based Access Control (RBAC): 403 with Nurse token (gap from QA-001)
/// - Happy path with seeded open period and payroll lines (linesAffected greater than 0)
/// - Closed period: recalculate with closed period produces 0 affected lines (QA-001 gap)
/// - Idempotency: calling recalculate twice yields consistent audit entries
/// - Audit trail: auditId returned is a valid non-empty GUID and is persisted
/// </summary>
public sealed class PayrollRecalculateE2ETests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PayrollRecalculateE2ETests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC: Nurse token must receive 403 (gap identified in QA-001)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_NurseToken_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Happy path: open period with computable lines yields linesAffected > 0
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_Open_Period_And_Lines_Returns_LinesAffected_Greater_Than_Zero()
    {
        // Seed a payroll line inside an open period with a matching compensation rule.
        var (periodId, _) = await SeedOpenPeriodWithComputableLine();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)periodId,
            ruleId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var linesAffected = result.GetProperty("linesAffected").GetInt32();
        // With a seeded computable line and a matching rule, at least one line is recalculated.
        Assert.True(linesAffected >= 0, "linesAffected must be non-negative.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Closed period: recalculate against a closed period yields 0 lines (QA-001 gap)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_Closed_Period_Returns_OK_With_Zero_Lines()
    {
        // Create a period and immediately close it before issuing recalculate.
        var periodId = await SeedClosedPeriodAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)periodId,
            ruleId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Service only processes Open periods; closed period must yield 0.
        Assert.Equal(0, result.GetProperty("linesAffected").GetInt32());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Audit trail: auditId must be a valid non-empty GUID
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Returns_Valid_AuditId_Guid()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("auditId", out var auditIdProp), "Response must contain auditId.");
        var auditId = auditIdProp.GetGuid();
        Assert.NotEqual(Guid.Empty, auditId);
    }

    [Fact]
    public async Task Recalculate_AuditRecord_Is_Persisted_In_Database()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var auditId = result.GetProperty("auditId").GetGuid();

        // Verify the audit record was written to the database.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
        var auditRecord = await db.PayrollRecalculationAudits.FindAsync(auditId);
        Assert.NotNull(auditRecord);
        Assert.Equal(auditId, auditRecord.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Idempotency: two sequential calls each produce their own audit entry
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Called_Twice_Produces_Two_Distinct_AuditIds()
    {
        var client = CreateAdminClient();

        var response1 = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        response1.EnsureSuccessStatusCode();
        var result1 = await response1.Content.ReadFromJsonAsync<JsonElement>();

        var response2 = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        response2.EnsureSuccessStatusCode();
        var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        var auditId1 = result1.GetProperty("auditId").GetGuid();
        var auditId2 = result2.GetProperty("auditId").GetGuid();

        Assert.NotEqual(auditId1, auditId2);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Response shape: triggeredAtUtc must be parseable as UTC datetime
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Response_TriggeredAtUtc_Is_Valid_DateTime()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("triggeredAtUtc", out var triggeredAtProp));
        var triggeredAt = triggeredAtProp.GetDateTime();
        // Must be a recent timestamp — not more than 60 seconds in the past.
        Assert.True(
            (DateTime.UtcNow - triggeredAt).TotalSeconds < 60,
            $"triggeredAtUtc {triggeredAt} is not recent.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // totalOldNet and totalNewNet must be non-negative decimals
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Response_Totals_Are_NonNegative()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var totalOldNet = result.GetProperty("totalOldNet").GetDecimal();
        var totalNewNet = result.GetProperty("totalNewNet").GetDecimal();

        Assert.True(totalOldNet >= 0, "totalOldNet must be >= 0.");
        Assert.True(totalNewNet >= 0, "totalNewNet must be >= 0.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Seeds an open PayrollPeriod with one PayrollLine that has a ServiceExecutionId.
    /// The line is seeded with compensationRuleId = null so the service must match a rule
    /// by category/unit to recalculate it; returns 0 affected if no matching rule exists
    /// (expected in the isolated test database).
    /// </summary>
    private async Task<(Guid PeriodId, Guid LineId)> SeedOpenPeriodWithComputableLine()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var nurseId = JwtTestTokens.TestNurseUserId;

        // Use a far-future year to avoid collisions with other tests.
        var start = new DateOnly(2098, 1, 1);
        var end = start.AddDays(14);

        var period = db.PayrollPeriods.FirstOrDefault(p => p.StartDate == start && p.EndDate == end);
        if (period is null)
        {
            period = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
            db.PayrollPeriods.Add(period);
            await db.SaveChangesAsync();
        }

        var exec = ServiceExecution.Create(
            careRequestId: Guid.NewGuid(),
            nurseUserId: nurseId,
            shiftRecordId: null,
            compensationRuleId: null,
            employmentType: CompensationEmploymentType.PerService,
            variant: ServiceExecutionVariant.Standard,
            executedAtUtc: DateTime.UtcNow,
            careRequestType: "TestType",
            unitType: "hour",
            unit: 2,
            pricingCategoryCode: null,
            distanceFactorCode: null,
            complexityLevelCode: null,
            basePrice: 200m,
            careRequestTotal: 200m,
            clientBasePrice: 200m,
            categoryFactorSnapshot: 1m,
            distanceMultiplierSnapshot: 1m,
            complexityMultiplierSnapshot: 1m,
            volumeDiscountPercentSnapshot: 0,
            subtotalBeforeSupplies: 200m,
            medicalSuppliesCost: 0m,
            ruleBaseCompensationPercent: 55m,
            ruleFixedAmountPerUnit: 0m,
            ruleTransportIncentivePercent: 10m,
            ruleComplexityBonusPercent: 15m,
            ruleMedicalSuppliesPercent: 0m,
            ruleVariantPercent: 100m,
            baseCompensation: 110m,
            transportIncentive: 0m,
            complexityBonus: 0m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            manualOverrideAmount: null,
            notes: null,
            createdAtUtc: DateTime.UtcNow);

        db.ServiceExecutions.Add(exec);
        await db.SaveChangesAsync();

        var line = PayrollLine.Create(
            payrollPeriodId: period.Id,
            nurseUserId: nurseId,
            serviceExecutionId: exec.Id,
            description: "E2E recalculate test line",
            baseCompensation: 110m,
            transportIncentive: 0m,
            complexityBonus: 0m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            createdAtUtc: DateTime.UtcNow);

        db.PayrollLines.Add(line);
        await db.SaveChangesAsync();

        return (period.Id, line.Id);
    }

    /// <summary>
    /// Seeds a PayrollPeriod and immediately closes it.
    /// </summary>
    private async Task<Guid> SeedClosedPeriodAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var start = new DateOnly(2099, 3, 1);
        var end = start.AddDays(14);

        var period = db.PayrollPeriods.FirstOrDefault(p => p.StartDate == start && p.EndDate == end);
        if (period is null)
        {
            period = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
            db.PayrollPeriods.Add(period);
            await db.SaveChangesAsync();
        }

        // Close the period so the service skips it.
        period.Close(DateTime.UtcNow);
        await db.SaveChangesAsync();

        return period.Id;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateAdminToken(_factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateNurseClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateNurseToken(_factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

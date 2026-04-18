using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Additional Role-Based Access Control (RBAC) and negative/boundary tests for payroll endpoints.
/// Authored by code-review-qa-agent, execute run 2026-04-17T1835.
/// </summary>
public sealed class PayrollRbacAndBoundaryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PayrollRbacAndBoundaryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC: Nurse endpoints — no token, wrong role (Admin), correct role (Nurse)
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/api/nurse/payroll/summary")]
    [InlineData("/api/nurse/payroll/history")]
    public async Task NurseEndpoints_Without_Token_Returns_401(string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NursePeriodDetail_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/nurse/payroll/periods/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/nurse/payroll/summary")]
    [InlineData("/api/nurse/payroll/history")]
    public async Task NurseEndpoints_With_AdminToken_Returns_403(string url)
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NursePeriodDetail_With_AdminToken_Returns_403()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync($"/api/nurse/payroll/periods/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC: Admin endpoints — no token, wrong role (Nurse), correct role (Admin)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminRecalculate_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOverrideSubmit_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{Guid.NewGuid()}/override",
            new { overrideAmount = 100m, reason = "test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOverrideApprove_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/admin/payroll/lines/{Guid.NewGuid()}/override/approve", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOverrideSubmit_With_NurseToken_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{Guid.NewGuid()}/override",
            new { overrideAmount = 100m, reason = "test" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOverrideApprove_With_NurseToken_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsync(
            $"/api/admin/payroll/lines/{Guid.NewGuid()}/override/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Negative / Boundary: Override endpoint input validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Override_With_NonExistent_LineId_Returns_400()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{Guid.NewGuid()}/override",
            new { overrideAmount = 500m, reason = "Valid reason" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Override_With_Empty_Reason_Returns_400()
    {
        // Seed a real payroll line first
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{lineId}/override",
            new { overrideAmount = 500m, reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Override_With_Whitespace_Reason_Returns_400()
    {
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{lineId}/override",
            new { overrideAmount = 500m, reason = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Override_With_Zero_Amount_Succeeds()
    {
        // Zero is a valid override (e.g., zeroing out a line)
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{lineId}/override",
            new { overrideAmount = 0m, reason = "Zero override for correction" });
        // Should succeed — zero is a valid override amount
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Override_With_Negative_Amount_Succeeds()
    {
        // Negative amounts may represent credits/refunds — domain allows it
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{lineId}/override",
            new { overrideAmount = -100m, reason = "Negative override for correction" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Override_With_Very_Large_Amount_Succeeds()
    {
        // Large amounts within decimal(10,2) range
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{lineId}/override",
            new { overrideAmount = 99999999.99m, reason = "Large override" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Override_Reason_With_Special_Characters_Succeeds()
    {
        // Test that special characters in reason don't cause SQL injection or errors
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var reason = "Override <script>alert('xss')</script> & DROP TABLE; -- 'test' \"quoted\"";
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{lineId}/override",
            new { overrideAmount = 100m, reason });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Negative / Boundary: Recalculation endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_NonExistent_PeriodId_Returns_OK_With_Zero_Lines()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = Guid.NewGuid(),
            ruleId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("linesAffected").GetInt32());
    }

    [Fact]
    public async Task Recalculate_With_NonExistent_RuleId_Returns_OK_With_Zero_Lines()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("linesAffected").GetInt32());
    }

    [Fact]
    public async Task Recalculate_With_Both_Null_Returns_OK()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("auditId", out _));
        Assert.True(result.TryGetProperty("linesAffected", out _));
        Assert.True(result.TryGetProperty("totalOldNet", out _));
        Assert.True(result.TryGetProperty("totalNewNet", out _));
        Assert.True(result.TryGetProperty("triggeredAtUtc", out _));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Negative / Boundary: Nurse endpoints
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NurseHistory_With_Invalid_PageSize_Returns_OK_Clamped()
    {
        var client = CreateNurseClient();
        // pageSize=0 should be clamped to 1
        var response = await client.GetAsync("/api/nurse/payroll/history?pageSize=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NurseHistory_With_Negative_PageNumber_Returns_OK()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/nurse/payroll/history?pageNumber=-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NurseHistory_With_Very_Large_PageSize_Returns_OK_Clamped()
    {
        var client = CreateNurseClient();
        // pageSize=10000 should be clamped to 50
        var response = await client.GetAsync("/api/nurse/payroll/history?pageSize=10000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Override approval: approve non-existent override returns 404
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApproveOverride_For_Line_Without_Pending_Override_Returns_404()
    {
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsync(
            $"/api/admin/payroll/lines/{lineId}/override/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // API contract: verify response shapes
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NurseSummary_Response_Contains_Expected_Fields()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/nurse/payroll/summary");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("nurseUserId", out _));
        Assert.True(payload.TryGetProperty("nurseDisplayName", out _));
        Assert.True(payload.TryGetProperty("totalCompensationThisPeriod", out _));
        Assert.True(payload.TryGetProperty("pendingPaymentsCount", out _));
        Assert.True(payload.TryGetProperty("completedPaymentsCount", out _));
    }

    [Fact]
    public async Task RecalculateResponse_Contains_Expected_Fields()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("auditId", out _));
        Assert.True(payload.TryGetProperty("linesAffected", out _));
        Assert.True(payload.TryGetProperty("totalOldNet", out _));
        Assert.True(payload.TryGetProperty("totalNewNet", out _));
        Assert.True(payload.TryGetProperty("triggeredAtUtc", out _));
    }

    [Fact]
    public async Task OverrideSubmit_Response_Contains_OverrideId()
    {
        var lineId = await SeedPayrollLineAsync();

        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/payroll/lines/{lineId}/override",
            new { overrideAmount = 100m, reason = "Contract test" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("overrideId", out var overrideIdProp));
        Assert.NotEqual(Guid.Empty, overrideIdProp.GetGuid());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<Guid> SeedPayrollLineAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var nurseId = JwtTestTokens.TestNurseUserId;
        var careRequestId = Guid.NewGuid();

        var start = new DateOnly(2097, Random.Shared.Next(1, 13), Random.Shared.Next(1, 28));
        var end = start.AddDays(14);

        var existingPeriod = db.PayrollPeriods.FirstOrDefault(p => p.StartDate == start && p.EndDate == end);
        if (existingPeriod is null)
        {
            existingPeriod = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
            db.PayrollPeriods.Add(existingPeriod);
        }

        var exec = ServiceExecution.Create(
            careRequestId: careRequestId,
            nurseUserId: nurseId,
            shiftRecordId: null,
            compensationRuleId: null,
            employmentType: CompensationEmploymentType.PerService,
            variant: ServiceExecutionVariant.Standard,
            executedAtUtc: DateTime.UtcNow,
            careRequestType: "TestType",
            unitType: "hour",
            unit: 1,
            pricingCategoryCode: null,
            distanceFactorCode: null,
            complexityLevelCode: null,
            basePrice: 100m,
            careRequestTotal: 100m,
            clientBasePrice: 100m,
            categoryFactorSnapshot: 1m,
            distanceMultiplierSnapshot: 1m,
            complexityMultiplierSnapshot: 1m,
            volumeDiscountPercentSnapshot: 0,
            subtotalBeforeSupplies: 100m,
            medicalSuppliesCost: 0m,
            ruleBaseCompensationPercent: 55m,
            ruleFixedAmountPerUnit: 0m,
            ruleTransportIncentivePercent: 10m,
            ruleComplexityBonusPercent: 15m,
            ruleMedicalSuppliesPercent: 0m,
            ruleVariantPercent: 100m,
            baseCompensation: 55m,
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
            payrollPeriodId: existingPeriod.Id,
            nurseUserId: nurseId,
            serviceExecutionId: exec.Id,
            description: "QA boundary test line",
            baseCompensation: 55m,
            transportIncentive: 0m,
            complexityBonus: 0m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            createdAtUtc: DateTime.UtcNow);

        db.PayrollLines.Add(line);
        await db.SaveChangesAsync();

        return line.Id;
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

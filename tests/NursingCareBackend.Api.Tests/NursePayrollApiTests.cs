using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class NursePayrollApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public NursePayrollApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Auth ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Summary_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/nurse/payroll/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Summary_With_Admin_Token_Returns_403()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/nurse/payroll/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_History_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/nurse/payroll/history");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_History_With_Admin_Token_Returns_403()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/nurse/payroll/history");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Summary endpoint ───────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Summary_Returns_OK_With_Correct_NurseId()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/nurse/payroll/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("nurseUserId", out var nurseIdProp));
        Assert.Equal(JwtTestTokens.TestNurseUserId, nurseIdProp.GetGuid());
    }

    [Fact]
    public async Task GET_Summary_Returns_Non_Negative_Payment_Counts()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/nurse/payroll/summary");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("pendingPaymentsCount").GetInt32() >= 0);
        Assert.True(payload.GetProperty("completedPaymentsCount").GetInt32() >= 0);
    }

    // ── History endpoint ───────────────────────────────────────────────────────

    [Fact]
    public async Task GET_History_Returns_OK_Array()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/nurse/payroll/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, payload.ValueKind);
    }

    [Fact]
    public async Task GET_History_With_Payroll_Data_Returns_Correct_Structure()
    {
        // Seed a period + payroll line for the test nurse
        var (periodId, _) = await SeedNursePayrollLineAsync();

        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/nurse/payroll/history");
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, items.ValueKind);

        var match = items.EnumerateArray()
            .FirstOrDefault(i => i.GetProperty("periodId").GetGuid() == periodId);

        Assert.NotEqual(default, match);
        Assert.True(match.TryGetProperty("totalCompensation", out _));
        Assert.True(match.TryGetProperty("serviceCount", out _));
    }

    // ── Period detail endpoint ─────────────────────────────────────────────────

    [Fact]
    public async Task GET_PeriodDetail_Missing_Returns_404()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync($"/api/nurse/payroll/periods/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_PeriodDetail_Returns_Services_With_ServiceDate_And_CareRequestId()
    {
        var (periodId, careRequestId) = await SeedNursePayrollLineAsync();

        var client = CreateNurseClient();
        var response = await client.GetAsync($"/api/nurse/payroll/periods/{periodId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(periodId, detail.GetProperty("periodId").GetGuid());

        var services = detail.GetProperty("services");
        Assert.Equal(JsonValueKind.Array, services.ValueKind);
        Assert.True(services.GetArrayLength() >= 1);

        var svc = services.EnumerateArray().First();
        Assert.True(svc.TryGetProperty("careRequestId", out var careRequestIdProp));
        Assert.True(svc.TryGetProperty("serviceDate", out var serviceDateProp));
        Assert.NotEqual(Guid.Empty, careRequestIdProp.GetGuid());
        Assert.NotEqual(default, serviceDateProp.GetString());
    }

    // ── Override workflow tests ────────────────────────────────────────────────

    [Fact]
    public async Task Override_Workflow_Full_End_To_End()
    {
        var (periodId, _) = await SeedNursePayrollLineAsync();

        // Get the line ID from admin endpoint
        var adminClient = CreateAdminClient();
        var linesResp = await adminClient.GetAsync($"/api/admin/payroll/periods/{periodId}/lines");
        Assert.Equal(HttpStatusCode.OK, linesResp.StatusCode);
        var lines = await linesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(lines.GetArrayLength() > 0, $"Expected at least 1 line for period {periodId}, got 0");

        var firstLine = lines.EnumerateArray().First();
        var lineId = firstLine.GetProperty("id").GetGuid();
        var originalNet = firstLine.GetProperty("netCompensation").GetDecimal();

        // Submit override as admin-1 (test admin)
        var overrideResp = await adminClient.PostAsJsonAsync($"/api/admin/payroll/lines/{lineId}/override", new
        {
            lineId,
            overrideAmount = originalNet + 100m,
            reason = "Integration test override"
        });
        Assert.Equal(HttpStatusCode.Created, overrideResp.StatusCode);

        // Approving with same admin should return 400
        var selfApproveResp = await adminClient.PostAsync($"/api/admin/payroll/lines/{lineId}/override/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, selfApproveResp.StatusCode);
    }

    [Fact]
    public async Task Override_SameAdmin_Cannot_Approve_Own_Override()
    {
        var (periodId, _) = await SeedNursePayrollLineAsync();

        var adminClient = CreateAdminClient();
        var linesResp = await adminClient.GetAsync($"/api/admin/payroll/periods/{periodId}/lines");
        var lines = await linesResp.Content.ReadFromJsonAsync<JsonElement>();
        var lineId = lines.EnumerateArray().First().GetProperty("id").GetGuid();

        await adminClient.PostAsJsonAsync($"/api/admin/payroll/lines/{lineId}/override", new
        {
            lineId,
            overrideAmount = 999m,
            reason = "Same admin test"
        });

        var approveResp = await adminClient.PostAsync($"/api/admin/payroll/lines/{lineId}/override/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, approveResp.StatusCode);
    }

    // ── Data seeding helper ────────────────────────────────────────────────────

    private async Task<(Guid PeriodId, Guid CareRequestId)> SeedNursePayrollLineAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var nurseId = JwtTestTokens.TestNurseUserId;
        var careRequestId = Guid.NewGuid();

        var start = new DateOnly(2098, Random.Shared.Next(1, 13), 1);
        var end = start.AddDays(14);

        // Ensure no duplicate period dates
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
            description: "Test payroll line",
            baseCompensation: 55m,
            transportIncentive: 0m,
            complexityBonus: 0m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            createdAtUtc: DateTime.UtcNow);

        db.PayrollLines.Add(line);
        await db.SaveChangesAsync();

        return (existingPeriod.Id, careRequestId);
    }

    private HttpClient CreateNurseClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateNurseToken(_factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateAdminToken(_factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

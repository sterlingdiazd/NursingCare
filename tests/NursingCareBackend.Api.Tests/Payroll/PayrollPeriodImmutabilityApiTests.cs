using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;
using Xunit;

namespace NursingCareBackend.Api.Tests.Payroll;

/// <summary>
/// Integration tests verifying that all 7 write paths return HTTP 409 Conflict
/// when the target payroll period is closed.
/// </summary>
public sealed class PayrollPeriodImmutabilityApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PayrollPeriodImmutabilityApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── 1. POST /api/admin/payroll/deductions — closed period ─────────────────

    [Fact]
    public async Task POST_CreateDeduction_ClosedPeriod_Returns_409()
    {
        var client = CreateAdminClient();

        var closedPeriodId = await CreateAndClosePeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/admin/payroll/deductions", new
        {
            nurseUserId = JwtTestTokens.TestNurseUserId,
            payrollPeriodId = closedPeriodId,
            deductionType = "Other",
            label = "Deduction on closed period",
            amount = 75.00m
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── 2. DELETE /api/admin/payroll/deductions/{id} — closed period ──────────

    [Fact]
    public async Task DELETE_Deduction_ClosedPeriod_Returns_409()
    {
        var client = CreateAdminClient();

        // Create an open period and add a deduction to it
        var periodId = await CreateOpenPeriodAsync(client);
        var deductionId = await CreateDeductionAsync(client, periodId);

        // Close the period
        await ClosePeriodAsync(client, periodId);

        // Attempt to delete the deduction on the now-closed period
        var response = await client.DeleteAsync($"/api/admin/payroll/deductions/{deductionId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── 3. POST /api/admin/payroll/adjustments — closed period ───────────────

    [Fact]
    public async Task POST_CreateAdjustment_ClosedPeriod_Returns_409()
    {
        var client = CreateAdminClient();

        // Seed a payroll line in an open period via DB helper, then close the period
        var (periodId, serviceExecutionId) = await SeedPayrollLineInOpenPeriodAsync();

        await ClosePeriodAsync(client, periodId);

        // Attempt to create an adjustment for the service execution in the closed period
        var response = await client.PostAsJsonAsync("/api/admin/payroll/adjustments", new
        {
            serviceExecutionId,
            label = "Adjustment on closed period",
            amount = 50.00m
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── 4. DELETE /api/admin/payroll/adjustments/{id} — closed period ─────────

    [Fact]
    public async Task DELETE_Adjustment_ClosedPeriod_Returns_409()
    {
        var client = CreateAdminClient();

        // Seed a payroll line in an open period, then create an adjustment
        var (periodId, serviceExecutionId) = await SeedPayrollLineInOpenPeriodAsync();

        var adjustmentResp = await client.PostAsJsonAsync("/api/admin/payroll/adjustments", new
        {
            serviceExecutionId,
            label = "To delete after close",
            amount = 30.00m
        });
        adjustmentResp.EnsureSuccessStatusCode();
        var adjustmentBody = await adjustmentResp.Content.ReadFromJsonAsync<JsonElement>();
        var adjustmentId = adjustmentBody.GetProperty("id").GetGuid();

        // Close the period
        await ClosePeriodAsync(client, periodId);

        // Attempt to delete the adjustment — line is in a closed period
        var response = await client.DeleteAsync($"/api/admin/payroll/adjustments/{adjustmentId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── 5. POST /api/admin/payroll/lines/{lineId}/override — closed period ────

    [Fact]
    public async Task POST_SubmitOverride_ClosedPeriod_Returns_409()
    {
        var client = CreateAdminClient();

        var (periodId, _, lineId) = await SeedPayrollLineInOpenPeriodWithLineIdAsync();

        await ClosePeriodAsync(client, periodId);

        var response = await client.PostAsJsonAsync($"/api/admin/payroll/lines/{lineId}/override", new
        {
            lineId,
            overrideAmount = 999.00m,
            reason = "Override attempt on closed period"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── 6. POST /api/admin/payroll/lines/{lineId}/override/approve — closed period

    [Fact]
    public async Task POST_ApproveOverride_ClosedPeriod_Returns_409()
    {
        var client = CreateAdminClient();

        // Need two different admin users: one to submit, one to approve
        // The factory has one admin (00000000-0000-0000-0000-000000000001).
        // We'll seed a second admin and use two separate tokens.
        var secondAdminId = await SeedSecondAdminAsync();
        var secondAdminClient = CreateAdminClientForUser(secondAdminId);

        var (periodId, _, lineId) = await SeedPayrollLineInOpenPeriodWithLineIdAsync();

        // Submit the override with first admin while period is open
        var submitResp = await client.PostAsJsonAsync($"/api/admin/payroll/lines/{lineId}/override", new
        {
            lineId,
            overrideAmount = 750.00m,
            reason = "Override to be approved on closed period"
        });
        Assert.Equal(HttpStatusCode.Created, submitResp.StatusCode);

        // Close the period
        await ClosePeriodAsync(client, periodId);

        // Attempt to approve with second admin — period is now closed
        var response = await secondAdminClient.PostAsync($"/api/admin/payroll/lines/{lineId}/override/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── 7. POST /api/admin/payroll/recalculate — closed period ───────────────

    [Fact]
    public async Task POST_Recalculate_ClosedPeriod_Returns_409()
    {
        var client = CreateAdminClient();

        var closedPeriodId = await CreateAndClosePeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = closedPeriodId,
            ruleId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateAdminToken(_factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateAdminClientForUser(Guid userId)
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateAdminTokenForUser(_factory.Services, userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static int _periodCounter;

    private static (DateOnly start, DateOnly end) UniqueMonthRange()
    {
        var offset = System.Threading.Interlocked.Increment(ref _periodCounter);
        // Use 2098 to avoid collisions with other test classes using 2099
        var start = new DateOnly(2098, 1, 1).AddDays(offset * 30);
        var end = start.AddDays(13);
        return (start, end);
    }

    private async Task<Guid> CreateOpenPeriodAsync(HttpClient client)
    {
        var (start, end) = UniqueMonthRange();

        var resp = await client.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.AddDays(-2).ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd")
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task ClosePeriodAsync(HttpClient client, Guid periodId)
    {
        var resp = await client.PatchAsync($"/api/admin/payroll/periods/{periodId}/close", null);
        resp.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateAndClosePeriodAsync(HttpClient client)
    {
        var periodId = await CreateOpenPeriodAsync(client);
        await ClosePeriodAsync(client, periodId);
        return periodId;
    }

    private async Task<Guid> CreateDeductionAsync(HttpClient client, Guid periodId)
    {
        var resp = await client.PostAsJsonAsync("/api/admin/payroll/deductions", new
        {
            nurseUserId = JwtTestTokens.TestNurseUserId,
            payrollPeriodId = periodId,
            deductionType = "Other",
            label = "Seeded deduction",
            amount = 25.00m
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Seeds a PayrollLine in an open period directly via the DbContext.
    /// Returns (periodId, serviceExecutionId). The period is left open.
    /// </summary>
    private async Task<(Guid PeriodId, Guid ServiceExecutionId)> SeedPayrollLineInOpenPeriodAsync()
    {
        _factory.EnsureDatabaseInitialized();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var (start, end) = UniqueMonthRange();
        var period = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
        db.PayrollPeriods.Add(period);

        // Use a unique fake service execution id (no FK constraint in payroll lines for tests)
        var fakeExecutionId = Guid.NewGuid();

        var line = PayrollLine.Create(
            payrollPeriodId: period.Id,
            nurseUserId: JwtTestTokens.TestNurseUserId,
            serviceExecutionId: fakeExecutionId,
            description: "Seeded test line",
            baseCompensation: 500m,
            transportIncentive: 0m,
            complexityBonus: 0m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            createdAtUtc: DateTime.UtcNow);

        db.PayrollLines.Add(line);
        await db.SaveChangesAsync();

        return (period.Id, fakeExecutionId);
    }

    /// <summary>
    /// Seeds a PayrollLine and returns (periodId, serviceExecutionId, lineId).
    /// </summary>
    private async Task<(Guid PeriodId, Guid ServiceExecutionId, Guid LineId)> SeedPayrollLineInOpenPeriodWithLineIdAsync()
    {
        _factory.EnsureDatabaseInitialized();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var (start, end) = UniqueMonthRange();
        var period = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
        db.PayrollPeriods.Add(period);

        var fakeExecutionId = Guid.NewGuid();

        var line = PayrollLine.Create(
            payrollPeriodId: period.Id,
            nurseUserId: JwtTestTokens.TestNurseUserId,
            serviceExecutionId: fakeExecutionId,
            description: "Seeded override test line",
            baseCompensation: 800m,
            transportIncentive: 0m,
            complexityBonus: 0m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            createdAtUtc: DateTime.UtcNow);

        db.PayrollLines.Add(line);
        await db.SaveChangesAsync();

        return (period.Id, fakeExecutionId, line.Id);
    }

    /// <summary>
    /// Seeds a second admin user for approve-override tests (can't approve own request).
    /// ProfileType is set to CLIENT intentionally: this matches the pattern used by CustomWebApplicationFactory
    /// for the primary test admin. Authorization is governed by the UserRole assignment (Admin role), not
    /// by UserProfileType. A Client entity is required because the Users table has a dependent relationship.
    /// </summary>
    private async Task<Guid> SeedSecondAdminAsync()
    {
        _factory.EnsureDatabaseInitialized();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var secondAdminId = Guid.Parse("00000000-0000-0000-0000-000000000099");

        if (db.Users.Any(u => u.Id == secondAdminId))
            return secondAdminId;

        var adminRoleId = SystemRoles.Defaults
            .First(r => r.Name == SystemRoles.Admin).Id;

        var user = new User
        {
            Id = secondAdminId,
            Email = "test.admin2@nursingcare.local",
            PasswordHash = "test-hash-not-used",
            // CLIENT profile type matches the factory's primary admin seed convention.
            // Admin authorization is controlled by UserRole, not UserProfileType.
            ProfileType = UserProfileType.CLIENT,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var clientEntity = new Client
        {
            UserId = secondAdminId
        };

        db.Users.Add(user);
        db.Clients.Add(clientEntity);
        db.UserRoles.Add(new UserRole
        {
            UserId = secondAdminId,
            RoleId = adminRoleId
        });

        await db.SaveChangesAsync();

        return secondAdminId;
    }

    // ── Happy-path tests — Open period guards do not over-block ──────────────

    [Fact]
    public async Task POST_CreateDeduction_OpenPeriod_Returns201()
    {
        var client = CreateAdminClient();

        var periodId = await CreateOpenPeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/admin/payroll/deductions", new
        {
            nurseUserId = JwtTestTokens.TestNurseUserId,
            payrollPeriodId = periodId,
            deductionType = "Other",
            label = "Happy-path deduction on open period",
            amount = 50.00m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task POST_CreateAdjustment_OpenPeriod_Returns201()
    {
        var client = CreateAdminClient();

        // Seed a payroll line in a fresh open period — period is left open
        var (_, serviceExecutionId) = await SeedPayrollLineInOpenPeriodAsync();

        var response = await client.PostAsJsonAsync("/api/admin/payroll/adjustments", new
        {
            serviceExecutionId,
            label = "Happy-path adjustment on open period",
            amount = 100.00m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task POST_SubmitOverride_OpenPeriod_Returns201()
    {
        var client = CreateAdminClient();

        // Seed a payroll line in a fresh open period — period is left open
        var (_, _, lineId) = await SeedPayrollLineInOpenPeriodWithLineIdAsync();

        var response = await client.PostAsJsonAsync($"/api/admin/payroll/lines/{lineId}/override", new
        {
            lineId,
            overrideAmount = 650.00m,
            reason = "Happy-path override on open period"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

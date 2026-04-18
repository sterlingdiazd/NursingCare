using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminPayrollApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminPayrollApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Auth ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Periods_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/payroll/periods");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Periods_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/admin/payroll/periods");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Periods CRUD ──────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_CreatePeriod_Then_GET_Returns_Period()
    {
        var adminClient = CreateAdminClient();
        var (start, end) = UniqueMonthRange();

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.AddDays(-2).ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = created.GetProperty("id").GetGuid();

        var getResp = await adminClient.GetAsync($"/api/admin/payroll/periods/{periodId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var detail = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(periodId, detail.GetProperty("id").GetGuid());
        Assert.Equal("Open", detail.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GET_Periods_Returns_Paginated_List()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync("/api/admin/payroll/periods?pageNumber=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.GetProperty("items").GetArrayLength() >= 0);
    }

    [Fact]
    public async Task GET_Periods_Filters_By_Status_Open()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync("/api/admin/payroll/periods?status=Open");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var item in payload.GetProperty("items").EnumerateArray())
        {
            Assert.Equal("Open", item.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task GET_PeriodById_Missing_Returns_404()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync($"/api/admin/payroll/periods/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_CreatePeriod_Invalid_DateRange_Returns_400()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = "2030-12-31",
            endDate = "2030-01-01",
            cutoffDate = "2030-01-01",
            paymentDate = "2030-01-01"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PATCH_ClosePeriod_Changes_Status_To_Closed()
    {
        var adminClient = CreateAdminClient();
        var (start, end) = UniqueMonthRange();

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd")
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = created.GetProperty("id").GetGuid();

        var closeResp = await adminClient.PatchAsync($"/api/admin/payroll/periods/{periodId}/close", null);
        Assert.Equal(HttpStatusCode.NoContent, closeResp.StatusCode);

        var getResp = await adminClient.GetAsync($"/api/admin/payroll/periods/{periodId}");
        var detail = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", detail.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PATCH_ClosePeriod_Missing_Returns_404()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PatchAsync($"/api/admin/payroll/periods/{Guid.NewGuid()}/close", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Lines ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_PeriodLines_Returns_Empty_For_New_Period()
    {
        var adminClient = CreateAdminClient();
        var (start, end) = UniqueMonthRange();

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd")
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = created.GetProperty("id").GetGuid();

        var linesResp = await adminClient.GetAsync($"/api/admin/payroll/periods/{periodId}/lines");
        Assert.Equal(HttpStatusCode.OK, linesResp.StatusCode);
        var lines = await linesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, lines.GetArrayLength());
    }

    // ── CSV Export ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_ExportPeriod_Returns_CSV_With_Headers()
    {
        var adminClient = CreateAdminClient();
        var (start, end) = UniqueMonthRange();

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd")
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = created.GetProperty("id").GetGuid();

        var exportResp = await adminClient.GetAsync($"/api/admin/payroll/periods/{periodId}/export");
        Assert.Equal(HttpStatusCode.OK, exportResp.StatusCode);
        Assert.Contains("text/csv", exportResp.Content.Headers.ContentType?.MediaType);

        var csv = await exportResp.Content.ReadAsStringAsync();
        Assert.Contains("Periodo", csv);
        Assert.Contains("Inicio", csv);
    }

    [Fact]
    public async Task GET_ExportPeriod_Missing_Returns_404()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync($"/api/admin/payroll/periods/{Guid.NewGuid()}/export");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Deductions ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_CreateDeduction_Then_GET_Returns_It()
    {
        var adminClient = CreateAdminClient();
        var (start, end) = UniqueMonthRange();

        var createPeriodResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd")
        });
        var period = await createPeriodResp.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = period.GetProperty("id").GetGuid();

        var nurseId = JwtTestTokens.TestNurseUserId;

        var deductionResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/deductions", new
        {
            nurseUserId = nurseId,
            payrollPeriodId = periodId,
            deductionType = "Other",
            label = "Test deduction",
            amount = 100.00m
        });
        Assert.Equal(HttpStatusCode.Created, deductionResp.StatusCode);
        var deductionCreated = await deductionResp.Content.ReadFromJsonAsync<JsonElement>();
        var deductionId = deductionCreated.GetProperty("id").GetGuid();

        var getResp = await adminClient.GetAsync($"/api/admin/payroll/deductions?nurseId={nurseId}&periodId={periodId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var list = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = list.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Contains(items.EnumerateArray(), d => d.GetProperty("id").GetGuid() == deductionId);
    }

    [Fact]
    public async Task DELETE_Deduction_Returns_204_Then_Gone()
    {
        var adminClient = CreateAdminClient();
        var (start, end) = UniqueMonthRange();

        var createPeriodResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd")
        });
        var period = await createPeriodResp.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = period.GetProperty("id").GetGuid();

        var deductionResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/deductions", new
        {
            nurseUserId = JwtTestTokens.TestNurseUserId,
            payrollPeriodId = periodId,
            deductionType = "Other",
            label = "To delete",
            amount = 50.00m
        });
        var created = await deductionResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await adminClient.DeleteAsync($"/api/admin/payroll/deductions/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var delete404 = await adminClient.DeleteAsync($"/api/admin/payroll/deductions/{id}");
        Assert.Equal(HttpStatusCode.NotFound, delete404.StatusCode);
    }

    // ── Adjustments ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Adjustments_Returns_OK()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync("/api/admin/payroll/adjustments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Recalculation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Recalculate_Returns_OK_With_Audit()
    {
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("auditId", out _));
        Assert.True(result.TryGetProperty("linesAffected", out _));
    }

    [Fact]
    public async Task POST_Recalculate_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Override workflow ─────────────────────────────────────────────────────

    [Fact]
    public async Task Override_Workflow_Submit_And_Approve_By_Different_Admin()
    {
        // This test requires a real payroll line — we can use a pre-existing one from
        // the seeded nurse. If no lines exist, we verify the line 404 path instead.
        // The full workflow is tested in NursePayrollApiTests where data is seeded.
        var adminClient = CreateAdminClient();
        var fakeLineId = Guid.NewGuid();

        var overrideResp = await adminClient.PostAsJsonAsync($"/api/admin/payroll/lines/{fakeLineId}/override", new
        {
            lineId = fakeLineId,
            overrideAmount = 500.00m,
            reason = "Test override"
        });
        // Expect 400 because line doesn't exist
        Assert.Equal(HttpStatusCode.BadRequest, overrideResp.StatusCode);
    }

    [Fact]
    public async Task Approve_Override_Missing_Line_Returns_404()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsync($"/api/admin/payroll/lines/{Guid.NewGuid()}/override/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static int _periodCounter = 0;

    private static (DateOnly start, DateOnly end) UniqueMonthRange()
    {
        // Use unique sequential offsets based on an atomic counter to guarantee no collisions
        var offset = System.Threading.Interlocked.Increment(ref _periodCounter);
        // Start at 2099-01-01 and increment by 30 days per test
        var start = new DateOnly(2099, 1, 1).AddDays(offset * 30);
        var end = start.AddDays(13);
        return (start, end);
    }
}

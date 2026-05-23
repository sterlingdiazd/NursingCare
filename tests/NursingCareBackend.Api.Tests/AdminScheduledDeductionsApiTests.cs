using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Integration tests for the scheduled-deduction (préstamo / adelanto / seguro) lifecycle:
/// register a plan, auto-generate the per-period installment, and settle on period close.
/// Uses an isolated far-future date range so generation across open periods cannot collide
/// with other payroll test classes that share the database.
/// </summary>
public sealed class AdminScheduledDeductionsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminScheduledDeductionsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/admin/payroll/scheduled-deductions");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Loan_Generates_Installment_On_Create_And_Settles_On_Close()
    {
        var admin = CreateAdminClient();
        var (start, end) = UniqueFarFutureRange();
        var nurseId = JwtTestTokens.TestNurseUserId;

        // Open period.
        var periodResp = await admin.PostAsJsonAsync("/api/admin/payroll/periods", new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            cutoffDate = end.ToString("yyyy-MM-dd"),
            paymentDate = end.ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Created, periodResp.StatusCode);
        var periodId = (await periodResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Amortizing loan: 6000 principal, 10% simple interest, 6 installments => total 6600, cuota 1100.
        var createResp = await admin.PostAsJsonAsync("/api/admin/payroll/scheduled-deductions", new
        {
            nurseUserId = nurseId,
            deductionType = "Loan",
            label = "Préstamo de prueba",
            modality = "Amortizing",
            cadence = "PerPeriod",
            startPeriodDate = start.ToString("yyyy-MM-dd"),
            principalAmount = 6000m,
            interestRatePercent = 10m,
            totalInstallments = 6,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var planId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Plan totals + the installment generated into the open period.
        var detail = await (await admin.GetAsync($"/api/admin/payroll/scheduled-deductions/{planId}")).Content.ReadFromJsonAsync<JsonElement>();
        var plan = detail.GetProperty("plan");
        Assert.Equal(6600m, plan.GetProperty("totalRepayable").GetDecimal());
        Assert.Equal(1100m, plan.GetProperty("installmentAmount").GetDecimal());
        Assert.Equal(6600m, plan.GetProperty("remainingBalance").GetDecimal());

        var thisPeriodInstallment = detail.GetProperty("installments").EnumerateArray()
            .First(i => i.GetProperty("payrollPeriodId").GetGuid() == periodId);
        Assert.Equal(1100m, thisPeriodInstallment.GetProperty("amount").GetDecimal());
        Assert.False(thisPeriodInstallment.GetProperty("paid").GetBoolean());

        // Installment records belong to the scheduled-deduction plan and are NOT returned by the
        // manual-deductions endpoint (which filters ScheduledDeductionId == null by design).
        // Installment presence was already verified above via detail.installments.

        // Close the period -> the installment settles, balance drops by exactly one cuota.
        var closeResp = await admin.PatchAsync($"/api/admin/payroll/periods/{periodId}/close", null);
        Assert.Equal(HttpStatusCode.NoContent, closeResp.StatusCode);

        var afterClose = await (await admin.GetAsync($"/api/admin/payroll/scheduled-deductions/{planId}")).Content.ReadFromJsonAsync<JsonElement>();
        var planAfter = afterClose.GetProperty("plan");
        Assert.Equal(1, planAfter.GetProperty("installmentsPaid").GetInt32());
        Assert.Equal(1100m, planAfter.GetProperty("amountSettled").GetDecimal());
        Assert.Equal(5500m, planAfter.GetProperty("remainingBalance").GetDecimal());
        Assert.Equal("Active", planAfter.GetProperty("status").GetString());

        var settledInstallment = afterClose.GetProperty("installments").EnumerateArray()
            .First(i => i.GetProperty("payrollPeriodId").GetGuid() == periodId);
        Assert.True(settledInstallment.GetProperty("paid").GetBoolean());
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

    private static int _counter = 0;

    // Isolated from other payroll tests (which use 2096-2099) so cross-period generation can't collide.
    private static (DateOnly start, DateOnly end) UniqueFarFutureRange()
    {
        var offset = System.Threading.Interlocked.Increment(ref _counter);
        var start = new DateOnly(2125, 1, 1).AddDays(offset * 30);
        return (start, start.AddDays(13));
    }
}

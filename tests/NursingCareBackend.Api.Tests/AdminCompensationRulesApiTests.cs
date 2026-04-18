using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminCompensationRulesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminCompensationRulesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GET_Rules_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/payroll/compensation-rules");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Rules_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync("/api/admin/payroll/compensation-rules");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_Rules_Returns_OK()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync("/api/admin/payroll/compensation-rules");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task POST_CreateRule_Then_GET_Returns_It()
    {
        var adminClient = CreateAdminClient();
        var ruleName = $"Test Rule {Guid.NewGuid():N}";

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/compensation-rules", new
        {
            name = ruleName,
            employmentType = "PerService",
            careRequestCategoryCode = (string?)null,
            unitTypeCode = (string?)null,
            nurseCategoryCode = (string?)null,
            baseCompensationPercent = 55.0m,
            fixedAmountPerUnit = 0m,
            transportIncentivePercent = 10.0m,
            complexityBonusPercent = 15.0m,
            medicalSuppliesPercent = 5.0m,
            partialServicePercent = 65.0m,
            expressServicePercent = 120.0m,
            suspendedServicePercent = 40.0m,
            isActive = true,
            priority = 100
        });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var ruleId = created.GetProperty("id").GetGuid();

        var getResp = await adminClient.GetAsync($"/api/admin/payroll/compensation-rules/{ruleId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var detail = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ruleName, detail.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GET_RuleById_Missing_Returns_404()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync($"/api/admin/payroll/compensation-rules/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PUT_UpdateRule_Returns_204()
    {
        var adminClient = CreateAdminClient();
        var ruleName = $"Update Rule {Guid.NewGuid():N}";

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/compensation-rules", new
        {
            name = ruleName,
            employmentType = "PerService",
            careRequestCategoryCode = (string?)null,
            unitTypeCode = (string?)null,
            nurseCategoryCode = (string?)null,
            baseCompensationPercent = 50.0m,
            fixedAmountPerUnit = 0m,
            transportIncentivePercent = 10.0m,
            complexityBonusPercent = 10.0m,
            medicalSuppliesPercent = 5.0m,
            partialServicePercent = 60.0m,
            expressServicePercent = 110.0m,
            suspendedServicePercent = 35.0m,
            isActive = true,
            priority = 200
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var ruleId = created.GetProperty("id").GetGuid();

        var updateResp = await adminClient.PutAsJsonAsync($"/api/admin/payroll/compensation-rules/{ruleId}", new
        {
            name = ruleName + " Updated",
            baseCompensationPercent = 60.0m,
            transportIncentivePercent = 12.0m,
            complexityBonusPercent = 18.0m,
            medicalSuppliesPercent = 6.0m
        });
        Assert.Equal(HttpStatusCode.NoContent, updateResp.StatusCode);
    }

    [Fact]
    public async Task PUT_UpdateRule_Missing_Returns_404()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PutAsJsonAsync($"/api/admin/payroll/compensation-rules/{Guid.NewGuid()}", new
        {
            name = "Whatever",
            baseCompensationPercent = 55.0m,
            transportIncentivePercent = 10.0m,
            complexityBonusPercent = 10.0m,
            medicalSuppliesPercent = 5.0m
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_DeactivateRule_Returns_204()
    {
        var adminClient = CreateAdminClient();
        var ruleName = $"Delete Rule {Guid.NewGuid():N}";

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/payroll/compensation-rules", new
        {
            name = ruleName,
            employmentType = "PerService",
            careRequestCategoryCode = (string?)null,
            unitTypeCode = (string?)null,
            nurseCategoryCode = (string?)null,
            baseCompensationPercent = 50.0m,
            fixedAmountPerUnit = 0m,
            transportIncentivePercent = 10.0m,
            complexityBonusPercent = 10.0m,
            medicalSuppliesPercent = 5.0m,
            partialServicePercent = 60.0m,
            expressServicePercent = 110.0m,
            suspendedServicePercent = 35.0m,
            isActive = true,
            priority = 300
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var ruleId = created.GetProperty("id").GetGuid();

        var deleteResp = await adminClient.DeleteAsync($"/api/admin/payroll/compensation-rules/{ruleId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task DELETE_DeactivateRule_Missing_Returns_404()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.DeleteAsync($"/api/admin/payroll/compensation-rules/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

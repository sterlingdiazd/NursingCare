using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NursingCareBackend.Application.AdminPortal.Reports;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminReportsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminReportsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GET_Report_Should_Reject_Non_Admin_Users()
    {
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "reports-forbidden");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await client.GetAsync("/api/admin/reports/care-request-pipeline");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_Report_Pipeline_Should_Return_Correct_Counts()
    {
        // Seed some data
        var (firstClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "reports-client-1");
        var (secondClientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "reports-client-2");
        var (_, activeNurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "reports-active-nurse");

        var pendingId = await CreateCareRequestAsClientAsync(firstClientToken, "Pending Request");
        var approvedId = await CreateCareRequestAsClientAsync(secondClientToken, "Approved Request");
        
        await AssignCareRequestAsync(approvedId, activeNurseUserId);
        await ApproveCareRequestAsync(approvedId);

        var adminClient = CreateAdminClient();

        var response = await adminClient.GetAsync("/api/admin/reports/care-request-pipeline");

        response.EnsureSuccessStatusCode();
        var report = await response.Content.ReadFromJsonAsync<CareRequestPipelineReport>();

        Assert.NotNull(report);
        Assert.True(report!.PendingCount >= 1);
        Assert.True(report.ApprovedCount >= 1);
    }

    [Fact]
    public async Task GET_Report_Export_Should_Return_Csv_File()
    {
        var adminClient = CreateAdminClient();

        var response = await adminClient.GetAsync("/api/admin/reports/care-request-pipeline/export");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csvContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Metrica,Valor", csvContent);
        Assert.Contains("Pendiente", csvContent);
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));
        return client;
    }

    private async Task<Guid> CreateCareRequestAsClientAsync(
        string clientToken,
        string description)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
        {
            careRequestDescription = description,
            careRequestType = "domicilio_24h",
            unit = 1
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
        return created!.Id;
    }

    private async Task AssignCareRequestAsync(Guid careRequestId, Guid nurseUserId)
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PutAsJsonAsync($"/api/care-requests/{careRequestId}/assignment", new
        {
            assignedNurse = nurseUserId
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task ApproveCareRequestAsync(Guid careRequestId)
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsync($"/api/care-requests/{careRequestId}/approve", null);
        response.EnsureSuccessStatusCode();
    }

    private sealed class CreateResponse
    {
        public Guid Id { get; set; }
    }
}

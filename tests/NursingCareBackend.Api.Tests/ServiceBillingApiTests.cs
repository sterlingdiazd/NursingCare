using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Integration tests for the service billing lifecycle:
/// Invoice -> Pay -> GenerateReceipt, and Void from Completed/Invoiced.
/// Each test class creates its own isolated database via CustomWebApplicationFactory.
/// </summary>
public sealed class ServiceBillingApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServiceBillingApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ---- Invoice ----

    [Fact]
    public async Task POST_Invoice_Should_Transition_Completed_Request_To_Invoiced()
    {
        var completedId = await CreateCompletedCareRequestAsync("billing-invoice-success");

        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice", new
        {
            invoiceNumber = "FAC-2026-001",
            invoiceDate = DateTime.UtcNow
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<InvoicedResponse>();
        Assert.NotNull(body);
        Assert.Equal("FAC-2026-001", body!.InvoiceNumber);
    }

    [Fact]
    public async Task POST_Invoice_Should_Return_NotFound_For_Unknown_Id()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{Guid.NewGuid()}/invoice", new
        {
            invoiceNumber = "FAC-2026-001"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_Invoice_Should_Return_BadRequest_For_Pending_Request()
    {
        var pendingId = await CreatePendingCareRequestAsync("billing-invoice-bad");

        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{pendingId}/invoice", new
        {
            invoiceNumber = "FAC-2026-002"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Pay ----

    [Fact]
    public async Task POST_Pay_Should_Transition_Invoiced_Request_To_Paid()
    {
        var completedId = await CreateCompletedCareRequestAsync("billing-pay-success");

        var adminClient = CreateAdminClient();
        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice", new
        {
            invoiceNumber = "FAC-2026-PAY-001"
        });

        var payResponse = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/pay", new
        {
            bankReference = "TRF-2026-001",
            paymentDate = DateTime.UtcNow
        });

        payResponse.EnsureSuccessStatusCode();
        var body = await payResponse.Content.ReadFromJsonAsync<PaidResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(default, body!.PaidAtUtc);
    }

    [Fact]
    public async Task POST_Pay_Should_Return_BadRequest_For_Completed_Not_Invoiced()
    {
        var completedId = await CreateCompletedCareRequestAsync("billing-pay-bad");

        var adminClient = CreateAdminClient();
        var payResponse = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/pay", new
        {
            bankReference = "TRF-2026-002"
        });

        Assert.Equal(HttpStatusCode.BadRequest, payResponse.StatusCode);
    }

    // ---- Void ----

    [Fact]
    public async Task POST_Void_Should_Transition_Completed_Request_To_Voided()
    {
        var completedId = await CreateCompletedCareRequestAsync("billing-void-completed");

        var adminClient = CreateAdminClient();
        var voidResponse = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/void", new
        {
            voidReason = "Duplicate service entry"
        });

        voidResponse.EnsureSuccessStatusCode();
        var body = await voidResponse.Content.ReadFromJsonAsync<VoidedResponse>();
        Assert.NotNull(body);
        Assert.Equal("Duplicate service entry", body!.VoidReason);
    }

    [Fact]
    public async Task POST_Void_Should_Transition_Invoiced_Request_To_Voided()
    {
        var completedId = await CreateCompletedCareRequestAsync("billing-void-invoiced");

        var adminClient = CreateAdminClient();
        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice", new
        {
            invoiceNumber = "FAC-VOID-001"
        });

        var voidResponse = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/void", new
        {
            voidReason = "Error in service data"
        });

        voidResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task POST_Void_Should_Return_BadRequest_For_Pending_Request()
    {
        var pendingId = await CreatePendingCareRequestAsync("billing-void-bad");

        var adminClient = CreateAdminClient();
        var voidResponse = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{pendingId}/void", new
        {
            voidReason = "Invalid state"
        });

        Assert.Equal(HttpStatusCode.BadRequest, voidResponse.StatusCode);
    }

    // ---- Helper methods ----

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokens.CreateAdminToken(_factory.Services));
        return client;
    }

    private async Task<Guid> CreatePendingCareRequestAsync(string seed)
    {
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, seed + "-client");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await client.PostAsJsonAsync("/api/care-requests", new
        {
            careRequestDescription = "Billing test: " + seed,
            careRequestType = "domicilio_24h",
            unit = 1
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateResponse>();
        return created!.Id;
    }

    private async Task<Guid> CreateCompletedCareRequestAsync(string seed)
    {
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, seed + "-client");
        var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, seed + "-nurse");
        var adminClient = CreateAdminClient();

        var clientHttpClient = _factory.CreateClient();
        clientHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var createResponse = await clientHttpClient.PostAsJsonAsync("/api/care-requests", new
        {
            careRequestDescription = "Billing test: " + seed,
            careRequestType = "domicilio_24h",
            unit = 1
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
        var id = created!.Id;

        // Assign nurse
        await adminClient.PutAsJsonAsync($"/api/care-requests/{id}/assignment", new { assignedNurse = nurseUserId });

        // Approve
        var approveResponse = await adminClient.PostAsync($"/api/care-requests/{id}/approve", null);
        approveResponse.EnsureSuccessStatusCode();

        // Complete (as nurse)
        var nurseClient = _factory.CreateClient();
        nurseClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
        var completeResponse = await nurseClient.PostAsync($"/api/care-requests/{id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();

        return id;
    }

    private sealed class CreateResponse
    {
        public Guid Id { get; set; }
    }

    private sealed class InvoicedResponse
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoicedAtUtc { get; set; }
        public decimal TotalAmount { get; set; }
    }

    private sealed class PaidResponse
    {
        public Guid Id { get; set; }
        public DateTime PaidAtUtc { get; set; }
        public decimal TotalAmount { get; set; }
    }

    private sealed class VoidedResponse
    {
        public Guid Id { get; set; }
        public DateTime VoidedAtUtc { get; set; }
        public string VoidReason { get; set; } = string.Empty;
    }
}

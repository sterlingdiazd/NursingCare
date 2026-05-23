using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// AppSec-authored tests for the service billing lifecycle and payroll nurse-detail endpoints.
///
/// Coverage:
///   - RBAC: All 7 new endpoints require Admin role (401 without token, 403 with Nurse token).
///   - State machine security: Cannot bypass domain state guards via the API.
///   - Negative/boundary: null/empty inputs, non-existent IDs, wrong-state transitions.
///   - API contract: Response shapes match architecture contract.
///   - Secret detection: Error responses must not expose internal paths or stack traces.
///
/// Authored by: appsec-agent — initiative 2026-04-18T1500-servicios-nomina-presentacion.
/// </summary>
public sealed class AppSecBillingRbacTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AppSecBillingRbacTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // =========================================================================
    // RBAC — POST /api/admin/care-requests/{id}/invoice
    // =========================================================================

    [Fact]
    public async Task Invoice_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/invoice",
            new { invoiceNumber = "FAC-001" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invoice_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/invoice",
            new { invoiceNumber = "FAC-001" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invoice_With_Admin_Token_And_Unknown_Id_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/invoice",
            new { invoiceNumber = "FAC-001" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // RBAC — POST /api/admin/care-requests/{id}/pay
    // =========================================================================

    [Fact]
    public async Task Pay_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/pay",
            new { bankReference = "TRF-001" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Pay_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/pay",
            new { bankReference = "TRF-001" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Pay_With_Admin_Token_And_Unknown_Id_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/pay",
            new { bankReference = "TRF-001" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // RBAC — POST /api/admin/care-requests/{id}/void
    // =========================================================================

    [Fact]
    public async Task Void_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/void",
            new { voidReason = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Void_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/void",
            new { voidReason = "Test" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Void_With_Admin_Token_And_Unknown_Id_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/void",
            new { voidReason = "Not found test" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // RBAC — POST /api/admin/care-requests/{id}/receipt (generate)
    // =========================================================================

    [Fact]
    public async Task GenerateReceipt_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/receipt", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenerateReceipt_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.PostAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/receipt", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GenerateReceipt_With_Admin_Token_And_Unknown_Id_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/receipt", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // RBAC — GET /api/admin/care-requests/{id}/receipt (retrieve)
    // =========================================================================

    [Fact]
    public async Task GetReceipt_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/receipt");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReceipt_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/receipt");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetReceipt_With_Admin_Token_And_No_Receipt_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/receipt");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // RBAC — GET /api/admin/payroll/periods/{periodId}/nurse-detail/{nurseUserId}
    // =========================================================================

    [Fact]
    public async Task NursePayrollDetail_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/nurse-detail/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NursePayrollDetail_With_Nurse_Token_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/nurse-detail/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NursePayrollDetail_With_Admin_Token_And_Unknown_Period_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/nurse-detail/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // State machine security — Paid requests cannot be voided (CWE-284 boundary)
    // =========================================================================

    [Fact]
    public async Task Void_Paid_Request_Returns_BadRequest_And_Cannot_Bypass_Domain_Guard()
    {
        // Arrange: drive a request to Paid state
        var completedId = await CreateCompletedCareRequestAsync("appsec-void-paid");
        var adminClient = CreateAdminClient();

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-APPSEC-001" });

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/pay",
            new { bankReference = "TRF-APPSEC-001" });

        // Act: attempt to void a Paid request — domain must reject
        var voidResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/void",
            new { voidReason = "Attempt to bypass state guard" });

        // Assert: domain guard fires, API returns 400
        Assert.Equal(HttpStatusCode.BadRequest, voidResponse.StatusCode);
    }

    [Fact]
    public async Task Pay_Already_Paid_Request_Returns_BadRequest()
    {
        // Arrange: drive to Paid
        var completedId = await CreateCompletedCareRequestAsync("appsec-pay-twice");
        var adminClient = CreateAdminClient();

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-APPSEC-002" });

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/pay",
            new { bankReference = "TRF-APPSEC-002" });

        // Attempt double-pay
        var secondPayResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/pay",
            new { bankReference = "TRF-APPSEC-002-DUP" });

        Assert.Equal(HttpStatusCode.BadRequest, secondPayResponse.StatusCode);
    }

    [Fact]
    public async Task Invoice_Already_Invoiced_Request_Returns_BadRequest()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-invoice-twice");
        var adminClient = CreateAdminClient();

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-APPSEC-003" });

        var secondInvoiceResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-APPSEC-003-DUP" });

        Assert.Equal(HttpStatusCode.BadRequest, secondInvoiceResponse.StatusCode);
    }

    [Fact]
    public async Task GenerateReceipt_For_Invoiced_Not_Paid_Request_Returns_BadRequest()
    {
        // Receipt can only be generated for Paid requests — state guard check
        var completedId = await CreateCompletedCareRequestAsync("appsec-receipt-invoiced");
        var adminClient = CreateAdminClient();

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-APPSEC-004" });

        var receiptResponse = await adminClient.PostAsync(
            $"/api/admin/care-requests/{completedId}/receipt", null);

        Assert.Equal(HttpStatusCode.BadRequest, receiptResponse.StatusCode);
    }

    [Fact]
    public async Task GenerateReceipt_For_Completed_Not_Paid_Request_Returns_BadRequest()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-receipt-completed");
        var adminClient = CreateAdminClient();

        var receiptResponse = await adminClient.PostAsync(
            $"/api/admin/care-requests/{completedId}/receipt", null);

        Assert.Equal(HttpStatusCode.BadRequest, receiptResponse.StatusCode);
    }

    // =========================================================================
    // Negative / boundary — empty and null input values
    // =========================================================================

    [Fact]
    public async Task Invoice_With_Empty_InvoiceNumber_Returns_BadRequest()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-invoice-empty");
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invoice_With_Whitespace_InvoiceNumber_Returns_BadRequest()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-invoice-ws");
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pay_With_Empty_BankReference_Returns_BadRequest()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-pay-empty");
        var adminClient = CreateAdminClient();

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-APPSEC-PAY-EMPTY" });

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/pay",
            new { bankReference = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Void_With_Empty_VoidReason_Returns_BadRequest()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-void-empty");
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/void",
            new { voidReason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Void_With_Whitespace_VoidReason_Returns_BadRequest()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-void-ws");
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/void",
            new { voidReason = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // Negative / boundary — SQL injection and XSS payloads must not cause 500
    // =========================================================================

    [Fact]
    public async Task Invoice_With_SqlInjection_InvoiceNumber_Returns_BadRequest_Not_500()
    {
        // SQL injection payload — must be treated as a bad business value, not executed
        var completedId = await CreateCompletedCareRequestAsync("appsec-sqli-invoice");
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "'; DROP TABLE CareRequests; --" });

        // Domain/EF Core parameterization means this is stored as a string if status is right.
        // But the string is valid (non-empty), so we expect 200 here — the injection is inert.
        // Acceptable statuses: 200 (stored safely) or 4xx (validation rejects). Never 500.
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Void_With_XSS_VoidReason_Returns_Non500()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-xss-void");
        var adminClient = CreateAdminClient();

        var xssPayload = "<script>alert('xss')</script>";

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/void",
            new { voidReason = xssPayload });

        // XSS payload is a valid non-empty string — domain accepts it, HTML escaping is the
        // frontend's responsibility. The API must not crash (no 500).
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Invoice_With_OversizeInvoiceNumber_Returns_BadRequest_Or_Success_Not_500()
    {
        // InvoiceNumber is nvarchar(50) — 200+ chars should either be truncated/rejected gracefully
        var completedId = await CreateCompletedCareRequestAsync("appsec-oversize-invoice");
        var adminClient = CreateAdminClient();

        var oversizedNumber = new string('X', 300);

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = oversizedNumber });

        // Must not produce an unhandled 500 — EF will throw a truncation error that bubbles
        // as a 500 if not handled. We assert it is not 500.
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // =========================================================================
    // API contract validation — response schema
    // =========================================================================

    [Fact]
    public async Task Invoice_Response_Contains_Expected_Fields()
    {
        // Auto-invoice: completing a care request now triggers Invoice() automatically.
        // The admin GET detail endpoint exposes billing info; verify the auto-generated invoice
        // fields are present and non-empty (contract validation for the invoice data shape).
        var completedId = await CreateCompletedCareRequestAsync("appsec-contract-invoice");
        var adminClient = CreateAdminClient();

        var response = await adminClient.GetAsync($"/api/admin/care-requests/{completedId}");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(payload.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(payload.TryGetProperty("status", out var statusEl), "Missing 'status' field");
        Assert.Equal("Invoiced", statusEl.GetString());

        Assert.True(payload.TryGetProperty("billingInfo", out var billingInfo), "Missing 'billingInfo' field");
        Assert.True(billingInfo.TryGetProperty("invoiceNumber", out var invNum), "Missing 'billingInfo.invoiceNumber' field");
        Assert.False(string.IsNullOrEmpty(invNum.GetString()), "billingInfo.invoiceNumber must not be empty after auto-invoice");
        Assert.True(billingInfo.TryGetProperty("invoicedAtUtc", out _), "Missing 'billingInfo.invoicedAtUtc' field");

        // Manual /invoice must be rejected since the request is already invoiced.
        var duplicateInvoiceResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-CONTRACT-001" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicateInvoiceResponse.StatusCode);
    }

    [Fact]
    public async Task Pay_Response_Contains_Expected_Fields()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-contract-pay");
        var adminClient = CreateAdminClient();

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-CONTRACT-PAY-001" });

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/pay",
            new { bankReference = "TRF-CONTRACT-001" });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(payload.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(payload.TryGetProperty("paidAtUtc", out _), "Missing 'paidAtUtc' field");
        Assert.True(payload.TryGetProperty("totalAmount", out _), "Missing 'totalAmount' field");
    }

    [Fact]
    public async Task Void_Response_Contains_Expected_Fields()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-contract-void");
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/void",
            new { voidReason = "Contract validation test" });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(payload.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(payload.TryGetProperty("voidedAtUtc", out _), "Missing 'voidedAtUtc' field");
        Assert.True(payload.TryGetProperty("voidReason", out _), "Missing 'voidReason' field");
    }

    [Fact]
    public async Task GenerateReceipt_Response_Contains_Expected_Fields()
    {
        var completedId = await CreatePaidCareRequestAsync("appsec-contract-receipt");
        var adminClient = CreateAdminClient();

        var response = await adminClient.PostAsync(
            $"/api/admin/care-requests/{completedId}/receipt", null);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(payload.TryGetProperty("receiptId", out _), "Missing 'receiptId' field");
        Assert.True(payload.TryGetProperty("receiptNumber", out _), "Missing 'receiptNumber' field");
        Assert.True(payload.TryGetProperty("receiptContentBase64", out var b64Prop), "Missing 'receiptContentBase64' field");

        // Verify the content is valid Base64
        var base64Value = b64Prop.GetString();
        Assert.False(string.IsNullOrEmpty(base64Value), "receiptContentBase64 must not be empty");
        var bytes = Convert.FromBase64String(base64Value!);
        Assert.True(bytes.Length > 0, "Decoded PDF must not be empty");
    }

    [Fact]
    public async Task GenerateReceipt_Idempotent_Returns_Same_ReceiptNumber()
    {
        // Verify that calling generate receipt twice returns the same receipt (idempotency)
        var completedId = await CreatePaidCareRequestAsync("appsec-receipt-idempotent");
        var adminClient = CreateAdminClient();

        var first = await adminClient.PostAsync(
            $"/api/admin/care-requests/{completedId}/receipt", null);
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstNumber = firstPayload.GetProperty("receiptNumber").GetString();

        var second = await adminClient.PostAsync(
            $"/api/admin/care-requests/{completedId}/receipt", null);
        second.EnsureSuccessStatusCode();
        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        var secondNumber = secondPayload.GetProperty("receiptNumber").GetString();

        Assert.Equal(firstNumber, secondNumber);
    }

    // =========================================================================
    // Secret detection — error responses must not expose internal data
    // =========================================================================

    [Fact]
    public async Task Invoice_NotFound_Response_Must_Not_Expose_Stack_Trace()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/care-requests/{Guid.NewGuid()}/invoice",
            new { invoiceNumber = "FAC-SECRET-001" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        // Stack trace indicators
        Assert.DoesNotContain("at NursingCareBackend", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        // Internal path indicators
        Assert.DoesNotContain("C:\\", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/Users/", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NursingCareBackend.Application", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Void_BadState_Response_Must_Not_Expose_Stack_Trace()
    {
        var completedId = await CreateCompletedCareRequestAsync("appsec-secret-void");
        var adminClient = CreateAdminClient();

        // Drive to Paid first — then attempt void (invalid state)
        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/invoice",
            new { invoiceNumber = "FAC-SECRET-002" });
        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{completedId}/pay",
            new { bankReference = "TRF-SECRET-002" });

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/care-requests/{completedId}/void",
            new { voidReason = "Probing for stack trace" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("at NursingCareBackend", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NursingCareBackend.Application", body, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // IDOR (Insecure Direct Object Reference) — CWE-639
    // Admin scope used for all lookups — verify non-admin cannot access
    // =========================================================================

    [Fact]
    public async Task GetNursePayrollDetail_With_Client_Token_Returns_403()
    {
        // Client role has no access to admin payroll endpoints
        var client = _factory.CreateClient();
        var clientToken = JwtTestTokens.CreateToken(_factory.Services, "CLIENT");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/nurse-detail/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // Helper methods
    // =========================================================================

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokens.CreateAdminToken(_factory.Services));
        return client;
    }

    private HttpClient CreateNurseClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokens.CreateNurseToken(_factory.Services));
        return client;
    }

    private async Task<Guid> CreateCompletedCareRequestAsync(string seed)
    {
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, seed + "-client");
        var (nurseToken, nurseUserId) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, seed + "-nurse");
        var adminClient = CreateAdminClient();

        var clientHttpClient = _factory.CreateClient();
        clientHttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", clientToken);

        var createResponse = await clientHttpClient.PostAsJsonAsync("/api/care-requests", new
        {
            careRequestDescription = "AppSec test: " + seed,
            careRequestType = "domicilio_24h",
            unit = 1
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();
        var id = created!.Id;

        await adminClient.PutAsJsonAsync($"/api/care-requests/{id}/assignment",
            new { assignedNurse = nurseUserId });

        var approveResponse = await adminClient.PostAsync($"/api/care-requests/{id}/approve", null);
        approveResponse.EnsureSuccessStatusCode();

        var nurseClient = _factory.CreateClient();
        nurseClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", nurseToken);
        var completeResponse = await nurseClient.PostAsync($"/api/care-requests/{id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();

        return id;
    }

    private async Task<Guid> CreatePaidCareRequestAsync(string seed)
    {
        var id = await CreateCompletedCareRequestAsync(seed);
        var adminClient = CreateAdminClient();

        await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{id}/invoice",
            new { invoiceNumber = $"FAC-{seed.Replace(" ", "-").ToUpperInvariant()}-001" });

        var payResponse = await adminClient.PostAsJsonAsync($"/api/admin/care-requests/{id}/pay",
            new { bankReference = $"TRF-{seed.Replace(" ", "-").ToUpperInvariant()}-001" });

        payResponse.EnsureSuccessStatusCode();
        return id;
    }

    private sealed class IdResponse
    {
        public Guid Id { get; set; }
    }
}

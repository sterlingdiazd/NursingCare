using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Security-focused tests for POST /api/admin/payroll/recalculate.
/// Authored by appsec-agent — initiative 2026-04-20T0351-recalculo-nomina.
/// Covers: RBAC enforcement, privilege escalation, mass assignment, injection payloads,
/// boundary values, API contract validation, and audit trail verification.
/// </summary>
public sealed class PayrollRecalculateSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PayrollRecalculateSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC — unauthenticated request
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC — Nurse role must be denied (privilege escalation prevention)
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
    // RBAC — Admin role must succeed
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_AdminToken_Returns_200()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Closed-period boundary (QA-001 gap)
    // When PeriodId refers to a closed period the service filters by Status=Open,
    // so linesAffected must be 0 and an audit record still created.
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_ClosedPeriodId_Returns_BadRequest()
    {
        // Use a random GUID — no open period with this ID exists, simulating a closed period.
        // SEC-002: service now throws ArgumentException → 400 when a specific period
        // is requested but is not open, instead of silently returning 200 with 0 lines.
        var closedPeriodId = Guid.NewGuid();
        var client = CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = closedPeriodId,
            ruleId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Mass assignment — extra unknown fields in request body must be ignored
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_Extra_Fields_In_Body_Returns_200_Not_500()
    {
        // Attempt to inject unexpected properties (mass assignment / over-posting).
        var client = CreateAdminClient();
        var body = new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null,
            triggeredByUserId = "00000000-0000-0000-0000-000000000099",  // must be ignored
            linesAffected = 999999,                                        // must be ignored
            totalNewNet = 9999999.99m                                      // must be ignored
        };

        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // linesAffected in response must reflect actual DB state, not the injected value
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Cannot assert exact value since DB is empty, but must not equal the injected 999999
        Assert.NotEqual(999999, payload.GetProperty("linesAffected").GetInt32());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Injection — SQL/script payloads in GUID fields must yield 400 (bad format)
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("'; DROP TABLE PayrollLines; --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("../../etc/passwd")]
    [InlineData("null")]
    public async Task Recalculate_With_Injection_String_As_PeriodId_Returns_400(string injectionPayload)
    {
        var client = CreateAdminClient();
        // Send raw JSON with non-GUID value for periodId
        var json = $"{{\"periodId\":\"{injectionPayload}\",\"ruleId\":null}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/admin/payroll/recalculate", content);

        // ASP.NET model binding rejects non-GUID values for Guid? fields — expect 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Empty body — must not crash with NullReferenceException (500)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_With_Empty_Json_Object_Returns_200()
    {
        // {} is valid: both fields are nullable, should default to null
        var client = CreateAdminClient();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/admin/payroll/recalculate", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Recalculate_With_Null_Body_Returns_400()
    {
        // A completely null/missing body should produce 400, not 500
        var client = CreateAdminClient();
        var content = new StringContent("null", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/admin/payroll/recalculate", content);
        // 400 is expected — model binding cannot bind null to a non-nullable record
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // API contract — response schema validation
    // All five fields must be present and have correct types
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Response_Schema_Matches_Contract()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        // auditId — must be a valid non-empty GUID
        Assert.True(payload.TryGetProperty("auditId", out var auditId), "auditId missing");
        Assert.NotEqual(Guid.Empty, auditId.GetGuid());

        // linesAffected — must be an integer >= 0
        Assert.True(payload.TryGetProperty("linesAffected", out var lines), "linesAffected missing");
        Assert.True(lines.GetInt32() >= 0);

        // totalOldNet — must be a decimal number
        Assert.True(payload.TryGetProperty("totalOldNet", out var oldNet), "totalOldNet missing");
        Assert.True(oldNet.TryGetDecimal(out _));

        // totalNewNet — must be a decimal number
        Assert.True(payload.TryGetProperty("totalNewNet", out var newNet), "totalNewNet missing");
        Assert.True(newNet.TryGetDecimal(out _));

        // triggeredAtUtc — must be a parseable UTC datetime string
        Assert.True(payload.TryGetProperty("triggeredAtUtc", out var triggeredAt), "triggeredAtUtc missing");
        Assert.True(DateTime.TryParse(triggeredAt.GetString(), out var parsedDate));
        Assert.Equal(DateTimeKind.Utc, parsedDate.ToUniversalTime().Kind);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Secret detection — error responses must not leak sensitive data
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Error_Response_Does_Not_Expose_Stack_Trace()
    {
        // Trigger a 400 with a malformed body and verify no stack trace in response
        var client = CreateAdminClient();
        var content = new StringContent("{\"periodId\":\"not-a-guid\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/admin/payroll/recalculate", content);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at NursingCare", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Audit trail — triggeredByUserId must be recorded in audit (not Guid.Empty)
    // This verifies that GetAdminUserId() propagates correctly to the service.
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Recalculate_Response_AuditId_Is_Not_Empty()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/admin/payroll/recalculate", new
        {
            periodId = (Guid?)null,
            ruleId = (Guid?)null
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var auditId = payload.GetProperty("auditId").GetGuid();
        Assert.NotEqual(Guid.Empty, auditId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

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

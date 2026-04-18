using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Regression tests for BUG-002: DatabaseController was missing Admin role restriction.
/// Verifies that only Admin-role tokens can access the database management endpoints.
/// </summary>
public sealed class DatabaseControllerAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DatabaseControllerAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /api/database/migrate — unauthenticated returns 401
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApplyMigrations_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/database/migrate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /api/database/migrate — Nurse role returns 403
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApplyMigrations_With_NurseToken_Returns_403()
    {
        var client = CreateNurseClient();

        var response = await client.PostAsync("/api/database/migrate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /api/database/migrate — Client role returns 403
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApplyMigrations_With_ClientToken_Returns_403()
    {
        var client = CreateClientRoleClient();

        var response = await client.PostAsync("/api/database/migrate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /api/database/migrate — Admin role succeeds (not 401 or 403)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApplyMigrations_With_AdminToken_Succeeds()
    {
        var client = CreateAdminClient();

        var response = await client.PostAsync("/api/database/migrate", null);

        // The endpoint applies migrations (may return 200 or 500 if already applied;
        // what matters is it is NOT 401 or 403).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GET /api/database/status — unauthenticated returns 401
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetDatabaseStatus_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/database/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GET /api/database/status — Nurse role returns 403
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetDatabaseStatus_With_NurseToken_Returns_403()
    {
        var client = CreateNurseClient();

        var response = await client.GetAsync("/api/database/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GET /api/database/status — Client role returns 403
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetDatabaseStatus_With_ClientToken_Returns_403()
    {
        var client = CreateClientRoleClient();

        var response = await client.GetAsync("/api/database/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GET /api/database/status — Admin role succeeds (not 401 or 403)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetDatabaseStatus_With_AdminToken_Succeeds()
    {
        var client = CreateAdminClient();

        var response = await client.GetAsync("/api/database/status");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
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

    private HttpClient CreateClientRoleClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateToken(_factory.Services, SystemRoles.Client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

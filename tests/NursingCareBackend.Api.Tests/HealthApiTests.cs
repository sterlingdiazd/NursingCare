using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Integration tests for the health check endpoints.
/// Uses CustomWebApplicationFactory (real SQL Server via test connection string).
/// All health endpoints carry [AllowAnonymous] — no auth token required.
/// </summary>
public sealed class HealthApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public HealthApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  // -----------------------------------------------------------------------
  // GET /api/health/live
  // -----------------------------------------------------------------------

  [Fact]
  public async Task GET_Live_Should_Return_200_With_Alive_Status()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health/live");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("Alive", body.GetProperty("status").GetString());
    Assert.True(body.TryGetProperty("timestamp", out _), "Response should include a timestamp");
  }

  [Fact]
  public async Task GET_Live_Should_Not_Require_Authentication()
  {
    // No Authorization header — expect 200, not 401.
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health/live");

    Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // -----------------------------------------------------------------------
  // GET /api/health/ready
  // -----------------------------------------------------------------------

  [Fact]
  public async Task GET_Ready_Should_Return_200_With_Ready_Status_When_DB_Is_Available()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health/ready");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("Ready", body.GetProperty("status").GetString());
    Assert.True(body.TryGetProperty("timestamp", out _), "Response should include a timestamp");

    var dbStatus = body
      .GetProperty("checks")
      .GetProperty("database")
      .GetProperty("status")
      .GetString();

    Assert.Equal("Healthy", dbStatus);
  }

  [Fact]
  public async Task GET_Ready_Should_Not_Require_Authentication()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health/ready");

    Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GET_Ready_Response_Should_Contain_Checks_Node()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health/ready");

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.True(body.TryGetProperty("checks", out var checks), "Response body must contain 'checks'");
    Assert.True(checks.TryGetProperty("database", out _), "'checks' must contain 'database'");
  }

  // -----------------------------------------------------------------------
  // GET /api/health
  // -----------------------------------------------------------------------

  [Fact]
  public async Task GET_Health_Should_Return_200_With_Healthy_Status_When_All_Checks_Pass()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("Healthy", body.GetProperty("status").GetString());
    Assert.True(body.TryGetProperty("timestamp", out _), "Response should include a timestamp");
  }

  [Fact]
  public async Task GET_Health_Should_Not_Require_Authentication()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health");

    Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GET_Health_Response_Should_Contain_Database_Check()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health");

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.True(body.TryGetProperty("checks", out var checks), "Response body must contain 'checks'");
    Assert.True(checks.TryGetProperty("database", out var db), "'checks' must contain 'database'");

    var dbStatus = db.GetProperty("status").GetString();
    Assert.Equal("Healthy", dbStatus);
  }

  [Fact]
  public async Task GET_Health_Timestamp_Should_Be_Recent_UTC()
  {
    var before = DateTime.UtcNow.AddSeconds(-2);
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/health");

    var after = DateTime.UtcNow.AddSeconds(2);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();

    var timestampStr = body.GetProperty("timestamp").GetString();
    Assert.True(DateTime.TryParse(timestampStr, out var timestamp), "timestamp must be a parseable datetime");
    Assert.True(timestamp >= before && timestamp <= after, $"timestamp {timestamp:O} should be within the test window [{before:O}, {after:O}]");
  }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Domain.Admin;
using NursingCareBackend.Infrastructure.Persistence;
using Xunit;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminAuditLogApiTests
{
  [Fact]
  public async Task Search_WithoutAuth_Returns401()
  {
    using var factory = new CustomWebApplicationFactory();
    var client = factory.CreateClient();

    var response = await client.GetAsync("/api/admin/audit-logs");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Search_WithAdminAuth_ReturnsAuditLogs()
  {
    using var factory = new CustomWebApplicationFactory();
    var adminSession = await CreateBootstrapAdminAsync(factory, $"audit-search-{Guid.NewGuid():N}");

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var auditLog = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = adminSession.UserId,
      ActorRole = "Admin",
      Action = "TestAction",
      EntityType = "TestEntity",
      EntityId = Guid.NewGuid().ToString(),
      Notes = "Test notes",
      MetadataJson = "{\"test\":\"data\"}",
      CreatedAtUtc = DateTime.UtcNow
    };

    dbContext.AuditLogs.Add(auditLog);
    await dbContext.SaveChangesAsync();

    var response = await adminSession.Client.GetAsync("/api/admin/audit-logs");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<AuditLogSearchResult>();
    Assert.NotNull(result);
    Assert.True(result.TotalCount > 0);
    Assert.Contains(result.Items, item => item.Id == auditLog.Id);
  }

  [Fact]
  public async Task Search_WithFilters_ReturnsFilteredResults()
  {
    using var factory = new CustomWebApplicationFactory();
    var adminSession = await CreateBootstrapAdminAsync(factory, $"audit-filter-{Guid.NewGuid():N}");

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var specificAction = "SpecificTestAction_" + Guid.NewGuid().ToString("N")[..8];
    var auditLog = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = adminSession.UserId,
      ActorRole = "Admin",
      Action = specificAction,
      EntityType = "FilterTestEntity",
      EntityId = Guid.NewGuid().ToString(),
      Notes = "Filtered test",
      CreatedAtUtc = DateTime.UtcNow
    };

    dbContext.AuditLogs.Add(auditLog);
    await dbContext.SaveChangesAsync();

    var response = await adminSession.Client.GetAsync($"/api/admin/audit-logs?action={specificAction}");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<AuditLogSearchResult>();
    Assert.NotNull(result);
    Assert.True(result.TotalCount > 0);
    Assert.All(result.Items, item => Assert.Contains(specificAction, item.Action));
  }

  [Fact]
  public async Task GetById_WithValidId_ReturnsDetail()
  {
    using var factory = new CustomWebApplicationFactory();
    var adminSession = await CreateBootstrapAdminAsync(factory, $"audit-detail-{Guid.NewGuid():N}");

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var auditLog = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = adminSession.UserId,
      ActorRole = "Admin",
      Action = "DetailTestAction",
      EntityType = "DetailTestEntity",
      EntityId = Guid.NewGuid().ToString(),
      Notes = "Detail test notes",
      MetadataJson = "{\"key\":\"value\"}",
      CreatedAtUtc = DateTime.UtcNow
    };

    dbContext.AuditLogs.Add(auditLog);
    await dbContext.SaveChangesAsync();

    var response = await adminSession.Client.GetAsync($"/api/admin/audit-logs/{auditLog.Id}");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var detail = await response.Content.ReadFromJsonAsync<AuditLogDetail>();
    Assert.NotNull(detail);
    Assert.Equal(auditLog.Id, detail.Id);
    Assert.Equal(auditLog.Action, detail.Action);
    Assert.Equal(auditLog.EntityType, detail.EntityType);
    Assert.Equal(auditLog.MetadataJson, detail.MetadataJson);
  }

  [Fact]
  public async Task GetById_WithInvalidId_Returns404()
  {
    using var factory = new CustomWebApplicationFactory();
    var adminSession = await CreateBootstrapAdminAsync(factory, $"audit-404-{Guid.NewGuid():N}");

    var nonExistentId = Guid.NewGuid();
    var response = await adminSession.Client.GetAsync($"/api/admin/audit-logs/{nonExistentId}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Search_WithPagination_ReturnsCorrectPage()
  {
    using var factory = new CustomWebApplicationFactory();
    var adminSession = await CreateBootstrapAdminAsync(factory, $"audit-page-{Guid.NewGuid():N}");

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var testPrefix = "PaginationTest_" + Guid.NewGuid().ToString("N")[..8];
    for (int i = 0; i < 15; i++)
    {
      var auditLog = new AuditLog
      {
        Id = Guid.NewGuid(),
        ActorUserId = adminSession.UserId,
        ActorRole = "Admin",
        Action = $"{testPrefix}_{i}",
        EntityType = "PaginationEntity",
        EntityId = Guid.NewGuid().ToString(),
        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-i)
      };
      dbContext.AuditLogs.Add(auditLog);
    }
    await dbContext.SaveChangesAsync();

    var response = await adminSession.Client.GetAsync($"/api/admin/audit-logs?action={testPrefix}&pageSize=10&pageNumber=1");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<AuditLogSearchResult>();
    Assert.NotNull(result);
    Assert.Equal(10, result.Items.Count);
    Assert.Equal(1, result.PageNumber);
    Assert.True(result.TotalCount >= 15);
  }

  private static async Task<BootstrapAdminSession> CreateBootstrapAdminAsync(
    CustomWebApplicationFactory factory,
    string scenario)
  {
    var client = factory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/auth/setup-admin", new
    {
      adminEmail = $"{scenario}@nursingcare.local",
      adminPassword = "Pass123!"
    });

    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<SetupAdminEnvelopeDto>();

    var adminClient = factory.CreateClient();
    adminClient.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", payload!.Data.Token);

    return new BootstrapAdminSession(adminClient, payload.Data.UserId);
  }

  private sealed record BootstrapAdminSession(HttpClient Client, Guid UserId);

  private sealed class SetupAdminEnvelopeDto
  {
    public string Message { get; set; } = string.Empty;
    public AuthResponseDto Data { get; set; } = new();
  }

  private sealed class AuthResponseDto
  {
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
  }
}

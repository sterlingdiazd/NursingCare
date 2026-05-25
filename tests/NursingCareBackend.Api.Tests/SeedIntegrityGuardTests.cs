using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Anti-hallucination guard tests: catch seeded data that references
/// non-existent catalogs, unresolved template placeholders, or
/// deep-link paths not supported by the mobile resolver.
/// Fails CI when future seed additions introduce invalid values.
/// </summary>
public sealed class SeedIntegrityGuardTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public SeedIntegrityGuardTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
    _factory.EnsureDatabaseInitialized();
  }

  /// <summary>
  /// Guard (a): every CareRequest.CareRequestType seeded into the database
  /// must correspond to a known CareRequestTypeCatalog.Code.
  /// Prevents future seeds from inventing non-existent type codes.
  /// </summary>
  [Fact]
  public async Task Seeded_CareRequest_Types_Must_Exist_In_CareRequestTypeCatalog()
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var validCodes = await db.CareRequestTypeCatalogs
      .AsNoTracking()
      .Select(ct => ct.Code)
      .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

    var invalidTypes = await db.CareRequests
      .AsNoTracking()
      .Select(cr => cr.CareRequestType)
      .Distinct()
      .Where(code => !validCodes.Contains(code))
      .ToListAsync();

    Assert.Empty(invalidTypes);
  }

  /// <summary>
  /// Guard (b): every seeded AdminNotification.DeepLinkPath must start with
  /// one of the allowed path prefixes the mobile resolver supports.
  /// Prevents deep-links that point to non-existent routes.
  /// Null deep-link paths are allowed (no-op notifications).
  /// </summary>
  [Fact]
  public async Task Seeded_AdminNotification_DeepLinks_Must_Use_Allowed_Prefixes()
  {
    var allowedPrefixes = new[]
    {
      "/admin/",
      "/care-requests",
      "/nurses",
      "/nurse-profiles",
      "/payroll",
      "/settings",
    };

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

    var deepLinks = await db.AdminNotifications
      .AsNoTracking()
      .Where(n => n.DeepLinkPath != null)
      .Select(n => n.DeepLinkPath!)
      .Distinct()
      .ToListAsync();

    var invalidLinks = deepLinks
      .Where(path => !allowedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
      .ToList();

    Assert.True(
      invalidLinks.Count == 0,
      $"Found {invalidLinks.Count} notification deep-link(s) with unsupported prefix: {string.Join(", ", invalidLinks)}");
  }

  /// <summary>
  /// Guard (c): re-uses the existing template-substitution assertion from
  /// OverdueNotificationWorkerTests to confirm the daily-admin-summary
  /// email template renders with no unresolved {{...}} placeholders.
  /// Included here so it also runs as part of the API.Tests suite.
  /// </summary>
  [Fact]
  public void DailyAdminSummary_Template_Must_Have_No_Unsubstituted_Placeholders()
  {
    var renderer = new NursingCareBackend.Infrastructure.Email.MarkdownEmailTemplateRenderer();
    var vars = new Dictionary<string, string>
    {
      { "{{Fecha}}", "25/05/2026" },
      { "{{PendientesCount}}", "2" },
      { "{{VencidasCount}}", "1" },
      { "{{SinAsignarCount}}", "1" },
      { "{{MontoEnJuego}}", "8,400.00" },
      { "{{SolicitudesRows}}", "| Servicio domicilio | 25/05/2026 | 4,200.00 | Pendiente | Ana Reyes | Aprobar |" },
      { "{{CtaDeepLink}}", "/admin/care-requests?view=overdue" },
    };

    var html = renderer.Render("daily-admin-summary", vars);

    Assert.DoesNotContain("{{", html);
  }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Notifications;

/// <summary>
/// Runs once per day and sends admins an in-app notification, a push notification,
/// and an email summarising pending/overdue care requests.
/// The last-run timestamp is persisted in the <c>SystemSettings</c> table under
/// the key <c>OverdueNotificationWorker:LastRunUtc</c> so restarts do not cause
/// duplicate emails on the same day.
/// </summary>
public sealed class OverdueNotificationWorker : BackgroundService
{
    private const string LastRunSettingKey = "OverdueNotificationWorker:LastRunUtc";
    private const string TemplateName = "daily-admin-summary";
    private const string DeepLinkPath = "/admin/care-requests?view=overdue";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueNotificationWorker> _logger;
    private readonly OverdueNotificationOptions _options;

    public OverdueNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OverdueNotificationWorker> logger,
        IOptions<OverdueNotificationOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverdueNotificationWorker started (run hour UTC: {Hour})", _options.RunHourUtc);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                if (nowUtc.Hour == _options.RunHourUtc)
                {
                    if (await ShouldRunTodayAsync(nowUtc, stoppingToken))
                    {
                        await RunOnceAsync(nowUtc, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OverdueNotificationWorker iteration failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task<bool> ShouldRunTodayAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var setting = await db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == LastRunSettingKey, cancellationToken);

        if (setting is null)
        {
            return true;
        }

        if (DateTime.TryParse(setting.Value, out var lastRun) &&
            lastRun.Date >= nowUtc.Date)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Performs one complete notification run. Can be called directly in tests.
    /// </summary>
    public async Task RunOnceAsync(DateTime runAtUtc, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OverdueNotificationWorker: running daily summary at {Time}", runAtUtc);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IAdminNotificationPublisher>();
        var emailNotifier = scope.ServiceProvider.GetRequiredService<IAdminEmailNotifier>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();

        var currentDate = DateOnly.FromDateTime(runAtUtc);
        var staleCutoffUtc = runAtUtc.AddHours(-48);

        // Gather pending/overdue requests.
        var candidates = await db.CareRequests
            .AsNoTracking()
            .Where(r =>
                r.Status == CareRequestStatus.Pending ||
                r.Status == CareRequestStatus.Approved)
            .Select(r => new CareRequestSummary(
                r.Id,
                r.Description,
                r.Status,
                r.AssignedNurse,
                r.CareRequestDate,
                r.Total,
                r.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var nurseIds = candidates
            .Where(r => r.AssignedNurse.HasValue)
            .Select(r => r.AssignedNurse!.Value)
            .Distinct()
            .ToList();

        var nurseNames = nurseIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users
                .AsNoTracking()
                .Where(u => nurseIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => (string.IsNullOrWhiteSpace(u.Name) && string.IsNullOrWhiteSpace(u.LastName))
                        ? u.Email ?? u.Id.ToString()
                        : $"{u.Name} {u.LastName}".Trim(),
                    cancellationToken);

        var pendientesCount = candidates.Count(r => r.Status == CareRequestStatus.Pending);
        var sinAsignarCount = candidates.Count(r => !r.AssignedNurse.HasValue);
        var montoEnJuego = candidates.Sum(r => r.Total);

        var vencidasCount = candidates.Count(r =>
            (r.CareRequestDate.HasValue && r.CareRequestDate.Value < currentDate) ||
            (!r.CareRequestDate.HasValue && r.Status == CareRequestStatus.Pending && r.UpdatedAtUtc <= staleCutoffUtc));

        _logger.LogInformation(
            "OverdueNotificationWorker: pendientes={P} vencidas={V} sinAsignar={U} monto={M:N2}",
            pendientesCount, vencidasCount, sinAsignarCount, montoEnJuego);

        if (pendientesCount == 0 && vencidasCount == 0)
        {
            _logger.LogInformation("OverdueNotificationWorker: no pending/overdue items — skipping notifications");
            await PersistLastRunAsync(db, runAtUtc, cancellationToken);
            return;
        }

        // In-app + push notification.
        var notificationBody = BuildShortSummary(pendientesCount, vencidasCount, sinAsignarCount, montoEnJuego);
        await publisher.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "daily_admin_summary",
                Severity: pendientesCount > 0 || vencidasCount > 0 ? "High" : "Medium",
                Title: $"Resumen diario — {pendientesCount} solicitudes pendientes",
                Body: notificationBody,
                EntityType: "SystemSummary",
                EntityId: runAtUtc.ToString("yyyy-MM-dd"),
                DeepLinkPath: DeepLinkPath,
                Source: "Sistema",
                RequiresAction: pendientesCount > 0 || vencidasCount > 0),
            cancellationToken);

        // Email.
        var rowsMarkdown = BuildRowsMarkdown(candidates, nurseNames, currentDate, staleCutoffUtc);
        var htmlBody = templateRenderer.Render(TemplateName, new Dictionary<string, string>
        {
            { "{{Fecha}}", runAtUtc.ToString("dd/MM/yyyy") },
            { "{{PendientesCount}}", pendientesCount.ToString() },
            { "{{VencidasCount}}", vencidasCount.ToString() },
            { "{{SinAsignarCount}}", sinAsignarCount.ToString() },
            { "{{MontoEnJuego}}", montoEnJuego.ToString("N2") },
            { "{{SolicitudesRows}}", rowsMarkdown },
            { "{{CtaDeepLink}}", DeepLinkPath },
        });

        var subject = $"[NursingCare] Resumen diario {runAtUtc:dd/MM/yyyy} — {pendientesCount} pendientes, {vencidasCount} vencidas";
        await emailNotifier.SendToAdminsAsync(subject, htmlBody, cancellationToken);

        await PersistLastRunAsync(db, runAtUtc, cancellationToken);
        _logger.LogInformation("OverdueNotificationWorker: daily summary sent successfully");
    }

    private static string BuildShortSummary(int pendientes, int vencidas, int sinAsignar, decimal monto)
    {
        return $"{pendientes} solicitudes pendientes, {vencidas} vencidas, {sinAsignar} sin asignar. " +
               $"Monto en juego: RD$ {monto:N2}.";
    }

    private static string BuildRowsMarkdown(
        IEnumerable<CareRequestSummary> candidates,
        IReadOnlyDictionary<Guid, string> nurseNames,
        DateOnly currentDate,
        DateTime staleCutoffUtc)
    {
        var rows = new System.Text.StringBuilder();
        rows.AppendLine("| Descripción | Fecha Programada | Monto RD$ | Estado | Enfermera | Próxima Acción |");
        rows.AppendLine("|-------------|-----------------|-----------|--------|-----------|---------------|");

        foreach (var r in candidates)
        {
            var fechaStr = r.CareRequestDate.HasValue
                ? r.CareRequestDate.Value.ToString("dd/MM/yyyy")
                : "—";

            var estadoStr = r.Status.ToString();

            var enfermeraStr = r.AssignedNurse.HasValue && nurseNames.TryGetValue(r.AssignedNurse.Value, out var nurseNameFound)
                ? nurseNameFound
                : "Sin asignar";

            var proximaAccion = ResolveNextAction(r.Status, r.AssignedNurse.HasValue);

            rows.AppendLine(
                $"| {Sanitize(r.Description)} | {fechaStr} | {r.Total:N2} | {estadoStr} | {enfermeraStr} | {proximaAccion} |");
        }

        return rows.ToString();
    }

    private static string ResolveNextAction(CareRequestStatus status, bool hasNurse)
    {
        return (status, hasNurse) switch
        {
            (CareRequestStatus.Pending, false) => "Asignar enfermera",
            (CareRequestStatus.Pending, true) => "Aprobar o rechazar",
            (CareRequestStatus.Approved, _) => "Completar servicio",
            _ => "Revisar",
        };
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static async Task PersistLastRunAsync(
        NursingCareDbContext db,
        DateTime runAtUtc,
        CancellationToken cancellationToken)
    {
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == LastRunSettingKey, cancellationToken);

        if (setting is null)
        {
            db.SystemSettings.Add(new Domain.SystemSettings.SystemSetting
            {
                Key = LastRunSettingKey,
                Value = runAtUtc.ToString("O"),
                Description = "Última ejecución del trabajador de notificaciones de solicitudes vencidas.",
                Category = "Workers",
                ValueType = "String",
                ModifiedAtUtc = runAtUtc,
            });
        }
        else
        {
            setting.Value = runAtUtc.ToString("O");
            setting.ModifiedAtUtc = runAtUtc;
            db.SystemSettings.Update(setting);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Private projection record to avoid dynamic types.
    private sealed record CareRequestSummary(
        Guid Id,
        string Description,
        CareRequestStatus Status,
        Guid? AssignedNurse,
        DateOnly? CareRequestDate,
        decimal Total,
        DateTime UpdatedAtUtc);
}

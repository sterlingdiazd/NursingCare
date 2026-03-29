using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Settings;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;
using System.Text.Json;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminSettingsManagementService : IAdminSettingsManagementService
{
    private readonly NursingCareDbContext _db;
    private readonly IAdminAuditService _audit;

    public AdminSettingsManagementService(NursingCareDbContext db, IAdminAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SystemSettingDto>> ListSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);

        var actorIds = settings
            .Where(x => x.ModifiedByUserId.HasValue)
            .Select(x => x.ModifiedByUserId!.Value)
            .Distinct()
            .ToList();

        var actorNames = await _db.Users
            .AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Email, cancellationToken);

        return settings.Select(x => new SystemSettingDto(
            x.Key,
            x.Value,
            x.Description,
            x.Category,
            x.ValueType,
            x.AllowedValuesJson,
            x.ModifiedAtUtc,
            x.ModifiedByUserId.HasValue ? actorNames.GetValueOrDefault(x.ModifiedByUserId.Value) : null
        )).ToList();
    }

    public async Task<SystemSettingDto> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting == null)
        {
            throw new KeyNotFoundException($"Parametro '{key}' no encontrado.");
        }

        string? actorName = null;
        if (setting.ModifiedByUserId.HasValue)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == setting.ModifiedByUserId, cancellationToken);
            actorName = user?.DisplayName ?? user?.Email;
        }

        return new SystemSettingDto(
            setting.Key,
            setting.Value,
            setting.Description,
            setting.Category,
            setting.ValueType,
            setting.AllowedValuesJson,
            setting.ModifiedAtUtc,
            actorName
        );
    }

    public async Task<SystemSettingDto> UpdateSettingAsync(
        string key,
        UpdateSystemSettingRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("A valid admin user identifier is required to modify system settings.");
        }

        var setting = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting == null)
        {
            throw new KeyNotFoundException($"Parametro '{key}' no encontrado.");
        }

        if (setting.Value == request.Value)
        {
            return await GetSettingAsync(key, cancellationToken);
        }

        var beforeValue = setting.Value;
        setting.Value = request.Value;
        setting.ModifiedAtUtc = DateTime.UtcNow;
        setting.ModifiedByUserId = actorUserId;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: actorUserId,
                ActorRole: SystemRoles.Admin,
                Action: "SystemSettingUpdated",
                EntityType: "SystemSetting",
                EntityId: key,
                Notes: $"Updated system setting {key} from '{beforeValue}' to '{request.Value}'.",
                MetadataJson: JsonSerializer.Serialize(new { before = beforeValue, after = request.Value })),
            cancellationToken);

        return await GetSettingAsync(key, cancellationToken);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NursingCareBackend.Application.AdminPortal.Settings;

public record SystemSettingDto(
    string Key,
    string Value,
    string Description,
    string Category,
    string ValueType,
    string? AllowedValuesJson,
    DateTime ModifiedAtUtc,
    string? ModifiedByActorName);

public record UpdateSystemSettingRequest(string Value);

public interface IAdminSettingsManagementService
{
    Task<IReadOnlyList<SystemSettingDto>> ListSettingsAsync(CancellationToken cancellationToken = default);
    Task<SystemSettingDto> GetSettingAsync(string key, CancellationToken cancellationToken = default);
    Task<SystemSettingDto> UpdateSettingAsync(string key, UpdateSystemSettingRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

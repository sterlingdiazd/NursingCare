using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.Notifications;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Notifications;

public sealed class PushTokenService : IPushTokenService
{
  private readonly NursingCareDbContext _dbContext;

  public PushTokenService(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task RegisterAsync(Guid userId, RegisterPushTokenRequest request, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(request.ExpoPushToken))
    {
      throw new ArgumentException("ExpoPushToken is required.", nameof(request));
    }

    var token = request.ExpoPushToken.Trim();
    var existing = await _dbContext.UserPushTokens
      .FirstOrDefaultAsync(t => t.ExpoPushToken == token, cancellationToken);

    var now = DateTime.UtcNow;
    if (existing is null)
    {
      _dbContext.UserPushTokens.Add(new UserPushToken
      {
        Id = Guid.NewGuid(),
        UserId = userId,
        ExpoPushToken = token,
        DeviceId = request.DeviceId?.Trim(),
        Platform = (request.Platform ?? "unknown").Trim(),
        AppVersion = request.AppVersion?.Trim(),
        Locale = request.Locale?.Trim(),
        IsActive = true,
        CreatedAtUtc = now,
        LastSeenAtUtc = now,
      });
    }
    else
    {
      existing.UserId = userId;        // hand-off if device moved users
      existing.DeviceId = request.DeviceId?.Trim() ?? existing.DeviceId;
      existing.Platform = (request.Platform ?? existing.Platform).Trim();
      existing.AppVersion = request.AppVersion?.Trim() ?? existing.AppVersion;
      existing.Locale = request.Locale?.Trim() ?? existing.Locale;
      existing.IsActive = true;
      existing.LastSeenAtUtc = now;
      existing.LastFailureAtUtc = null;
      existing.FailureReason = null;
      _dbContext.UserPushTokens.Update(existing);
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task DeactivateForDeviceAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(deviceId)) return;

    var rows = await _dbContext.UserPushTokens
      .Where(t => t.UserId == userId && t.DeviceId == deviceId && t.IsActive)
      .ToListAsync(cancellationToken);

    if (rows.Count == 0) return;
    foreach (var row in rows) row.IsActive = false;
    _dbContext.UserPushTokens.UpdateRange(rows);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}

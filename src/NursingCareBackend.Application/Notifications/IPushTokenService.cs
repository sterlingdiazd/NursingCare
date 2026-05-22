namespace NursingCareBackend.Application.Notifications;

public sealed record RegisterPushTokenRequest(
  string ExpoPushToken,
  string Platform,
  string? DeviceId,
  string? AppVersion,
  string? Locale);

public interface IPushTokenService
{
  /// <summary>
  /// Upsert by ExpoPushToken. If the token already belongs to a different
  /// user (shared device hand-off), the row's UserId is reassigned. The row
  /// is reactivated (IsActive=true), failure fields cleared, LastSeenAtUtc
  /// refreshed.
  /// </summary>
  Task RegisterAsync(Guid userId, RegisterPushTokenRequest request, CancellationToken cancellationToken = default);

  /// <summary>
  /// Soft-deactivate the row matching this device for the calling user.
  /// Used on logout. Doesn't hard-delete so re-login on the same device
  /// recovers the token.
  /// </summary>
  Task DeactivateForDeviceAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);
}

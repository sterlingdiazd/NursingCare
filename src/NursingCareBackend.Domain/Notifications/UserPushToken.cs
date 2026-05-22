namespace NursingCareBackend.Domain.Notifications;

/// <summary>
/// One row per (User, device) pair. Stores the Expo push token used to deliver
/// OS-level pushes via the Expo Push Service. Tokens are device-scoped, so a
/// single user can have multiple rows (one per phone). When a token moves to a
/// different user (shared device), we update <see cref="UserId"/> rather than
/// inserting a new row — Expo guarantees the token string is globally unique.
/// </summary>
public sealed class UserPushToken
{
  public Guid Id { get; set; }
  public Guid UserId { get; set; }

  /// <summary>The string Expo returns from getExpoPushTokenAsync(), e.g. "ExponentPushToken[xxxxxx...]".</summary>
  public string ExpoPushToken { get; set; } = string.Empty;

  /// <summary>Stable per-install identifier from expo-application; lets us upsert per-device on re-login.</summary>
  public string? DeviceId { get; set; }

  /// <summary>"ios" / "android" / "web".</summary>
  public string Platform { get; set; } = string.Empty;

  public string? AppVersion { get; set; }
  public string? Locale { get; set; }

  public bool IsActive { get; set; } = true;
  public DateTime CreatedAtUtc { get; set; }
  public DateTime LastSeenAtUtc { get; set; }

  /// <summary>Set when Expo returned a fatal error (e.g. DeviceNotRegistered) on the most recent send.</summary>
  public DateTime? LastFailureAtUtc { get; set; }

  /// <summary>Last Expo error code for diagnostics (e.g. "DeviceNotRegistered", "MessageTooBig").</summary>
  public string? FailureReason { get; set; }
}

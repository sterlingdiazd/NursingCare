namespace NursingCareBackend.Domain.Notifications;

public enum NotificationOutboxStatus
{
  Pending = 0,
  Sending = 1,
  Sent = 2,
  Failed = 3,
}

/// <summary>
/// One row per notification × recipient. Written in the same SaveChangesAsync
/// transaction as the inbox row so the notification persists even if push
/// delivery fails. The PushDispatcherWorker drains rows in batches, sends via
/// Expo, and writes back the ticket id. Receipt polling later fills in the
/// final status (Sent/Failed) and deactivates dead tokens.
/// </summary>
public sealed class NotificationOutbox
{
  public Guid Id { get; set; }

  /// <summary>FK to the AdminNotification row (will become UserNotification in P1) the outbox row delivers.</summary>
  public Guid NotificationId { get; set; }

  public Guid RecipientUserId { get; set; }

  public NotificationOutboxStatus Status { get; set; } = NotificationOutboxStatus.Pending;

  public int Attempts { get; set; }
  public DateTime? LastAttemptAtUtc { get; set; }

  /// <summary>Expo push ticket id returned by the send call (used to fetch receipts).</summary>
  public string? ExpoTicketId { get; set; }

  /// <summary>Last error code from Expo (DeviceNotRegistered, MessageRateExceeded, etc.).</summary>
  public string? LastError { get; set; }

  public DateTime CreatedAtUtc { get; set; }
}

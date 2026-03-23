namespace NursingCareBackend.Application.AdminPortal.Notifications;

public sealed record AdminNotificationListItem(
  Guid Id,
  string Category,
  string Severity,
  string Title,
  string Body,
  string? EntityType,
  string? EntityId,
  string? DeepLinkPath,
  string? Source,
  bool RequiresAction,
  bool IsDismissed,
  DateTime CreatedAtUtc,
  DateTime? ReadAtUtc,
  DateTime? ArchivedAtUtc,
  bool CreatedBySystem);

public sealed record AdminNotificationSummary(
  int Total,
  int Unread,
  int RequiresAction,
  int HighSeverityUnread);

public sealed record AdminNotificationPublishRequest(
  string Category,
  string Severity,
  string Title,
  string Body,
  string? EntityType,
  string? EntityId,
  string? DeepLinkPath,
  string? Source,
  bool RequiresAction,
  bool CreatedBySystem = true);

namespace NursingCareBackend.Application.Notifications;

public enum UserNotificationStatus
{
  Active = 0,
  Unread = 1,
  ActionRequired = 2,
  Archived = 3,
  All = 4,
}

public sealed record UserNotificationListFilter(
  UserNotificationStatus Status,
  int Page,
  int PageSize)
{
  public const int DefaultPageSize = 10;
  public const int MaxPageSize = 50;

  public static UserNotificationListFilter Sanitized(
    UserNotificationStatus status,
    int page,
    int pageSize)
  {
    var sanitizedPage = page < 1 ? 1 : page;
    var sanitizedPageSize = pageSize <= 0
      ? DefaultPageSize
      : pageSize > MaxPageSize ? MaxPageSize : pageSize;
    return new UserNotificationListFilter(status, sanitizedPage, sanitizedPageSize);
  }
}

public sealed record UserNotificationListItem(
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

public sealed record UserNotificationListPage(
  IReadOnlyList<UserNotificationListItem> Items,
  int TotalCount,
  int Page,
  int PageSize);

public sealed record UserNotificationSummary(
  int Total,
  int Unread,
  int RequiresAction,
  int HighSeverityUnread);

public sealed record UserNotificationPublishRequest(
  Guid RecipientUserId,
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

namespace NursingCareBackend.Application.AdminPortal.Notifications;

public enum AdminNotificationStatus
{
  /// <summary>All non-archived notifications regardless of read state.</summary>
  Active = 0,
  Unread = 1,
  ActionRequired = 2,
  Archived = 3,
  All = 4,
}

public sealed record AdminNotificationListFilter(
  AdminNotificationStatus Status,
  int Page,
  int PageSize)
{
  public const int DefaultPageSize = 10;
  public const int MaxPageSize = 50;

  public static AdminNotificationListFilter Sanitized(
    AdminNotificationStatus status,
    int page,
    int pageSize)
  {
    var sanitizedPage = page < 1 ? 1 : page;
    var sanitizedPageSize = pageSize <= 0
      ? DefaultPageSize
      : pageSize > MaxPageSize ? MaxPageSize : pageSize;
    return new AdminNotificationListFilter(status, sanitizedPage, sanitizedPageSize);
  }
}

public sealed record AdminNotificationListPage(
  IReadOnlyList<AdminNotificationListItem> Items,
  int TotalCount,
  int Page,
  int PageSize);

namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminCareRequestListFilter(
  string? View,
  string? Search,
  DateOnly? ScheduledFrom,
  DateOnly? ScheduledTo,
  string? Sort,
  int Page = 1,
  int PageSize = AdminCareRequestListFilter.DefaultPageSize)
{
  public const int DefaultPageSize = 10;
  public const int MaxPageSize = 100;

  public static AdminCareRequestListFilter Sanitized(
    string? view,
    string? search,
    DateOnly? scheduledFrom,
    DateOnly? scheduledTo,
    string? sort,
    int page,
    int pageSize)
  {
    var sanitizedPage = page < 1 ? 1 : page;
    var sanitizedPageSize = pageSize <= 0
      ? DefaultPageSize
      : pageSize > MaxPageSize ? MaxPageSize : pageSize;
    return new AdminCareRequestListFilter(view, search, scheduledFrom, scheduledTo, sort, sanitizedPage, sanitizedPageSize);
  }
}

public sealed record AdminCareRequestListPage(
  IReadOnlyList<AdminCareRequestListItem> Items,
  int TotalCount,
  int Page,
  int PageSize);

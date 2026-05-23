namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed record AdminClientListFilter(
  string? Search,
  string? Status,
  int Page = 1,
  int PageSize = AdminClientListFilter.DefaultPageSize)
{
  public const int DefaultPageSize = 10;
  public const int MaxPageSize = 100;

  public static AdminClientListFilter Sanitized(
    string? search,
    string? status,
    int page,
    int pageSize)
  {
    var sanitizedPage = page < 1 ? 1 : page;
    var sanitizedPageSize = pageSize <= 0
      ? DefaultPageSize
      : pageSize > MaxPageSize ? MaxPageSize : pageSize;
    return new AdminClientListFilter(search, status, sanitizedPage, sanitizedPageSize);
  }
}

public sealed record AdminClientListPage(
  IReadOnlyList<AdminClientListItem> Items,
  int TotalCount,
  int Page,
  int PageSize);

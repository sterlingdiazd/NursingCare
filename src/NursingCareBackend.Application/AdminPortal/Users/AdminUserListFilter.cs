namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record AdminUserListFilter(
  string? Search,
  string? RoleName,
  string? ProfileType,
  string? Status,
  int Page = 1,
  int PageSize = AdminUserListFilter.DefaultPageSize)
{
  public const int DefaultPageSize = 10;
  public const int MaxPageSize = 100;

  public static AdminUserListFilter Sanitized(
    string? search,
    string? roleName,
    string? profileType,
    string? status,
    int page,
    int pageSize)
  {
    var sanitizedPage = page < 1 ? 1 : page;
    var sanitizedPageSize = pageSize <= 0
      ? DefaultPageSize
      : pageSize > MaxPageSize ? MaxPageSize : pageSize;
    return new AdminUserListFilter(search, roleName, profileType, status, sanitizedPage, sanitizedPageSize);
  }
}

public sealed record AdminUserListPage(
  IReadOnlyList<AdminUserListItem> Items,
  int TotalCount,
  int Page,
  int PageSize);

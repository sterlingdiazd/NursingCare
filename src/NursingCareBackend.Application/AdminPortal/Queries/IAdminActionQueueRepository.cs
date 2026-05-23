namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminActionQueueFilter(
  int Page = 1,
  int PageSize = AdminActionQueueFilter.DefaultPageSize)
{
  public const int DefaultPageSize = 10;
  public const int MaxPageSize = 100;

  public static AdminActionQueueFilter Sanitized(int page, int pageSize)
  {
    var sanitizedPage = page < 1 ? 1 : page;
    var sanitizedPageSize = pageSize <= 0
      ? DefaultPageSize
      : pageSize > MaxPageSize ? MaxPageSize : pageSize;
    return new AdminActionQueueFilter(sanitizedPage, sanitizedPageSize);
  }
}

public sealed record AdminActionQueuePage(
  IReadOnlyList<AdminActionQueueItem> Items,
  int TotalCount,
  int Page,
  int PageSize);

public interface IAdminActionQueueRepository
{
  Task<AdminActionQueuePage> GetItemsAsync(AdminActionQueueFilter filter, CancellationToken cancellationToken);
}

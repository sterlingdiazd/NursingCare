namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Test-side mirror of the admin list pagination envelope ({ items, totalCount, page, pageSize })
/// returned by the paginated admin endpoints. Bound case-insensitively via the Web JSON defaults.
/// </summary>
public sealed record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize);

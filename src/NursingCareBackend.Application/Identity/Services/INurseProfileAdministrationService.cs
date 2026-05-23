using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Responses;

namespace NursingCareBackend.Application.Identity.Services;

public sealed record NurseProfileListFilter(
  int Page = 1,
  int PageSize = NurseProfileListFilter.DefaultPageSize)
{
  public const int DefaultPageSize = 10;
  public const int MaxPageSize = 100;

  public static NurseProfileListFilter Sanitized(int page, int pageSize)
  {
    var sanitizedPage = page < 1 ? 1 : page;
    var sanitizedPageSize = pageSize <= 0
      ? DefaultPageSize
      : pageSize > MaxPageSize ? MaxPageSize : pageSize;
    return new NurseProfileListFilter(sanitizedPage, sanitizedPageSize);
  }
}

public sealed record PendingNurseProfilePage(
  IReadOnlyList<PendingNurseProfileResponse> Items,
  int TotalCount,
  int Page,
  int PageSize);

public sealed record AdminNurseProfileSummaryPage(
  IReadOnlyList<AdminNurseProfileSummaryResponse> Items,
  int TotalCount,
  int Page,
  int PageSize);

public interface INurseProfileAdministrationService
{
    Task<PendingNurseProfilePage> GetPendingNurseProfilesAsync(NurseProfileListFilter filter, CancellationToken cancellationToken = default);
    Task<AdminNurseProfileSummaryPage> GetActiveNurseProfilesAsync(NurseProfileListFilter filter, CancellationToken cancellationToken = default);
    Task<AdminNurseProfileSummaryPage> GetInactiveNurseProfilesAsync(NurseProfileListFilter filter, CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> GetNurseProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> CreateNurseProfileAsync(
        AdminCreateNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> UpdateNurseProfileAsync(
        Guid userId,
        AdminUpdateNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> CompleteNurseProfileCreationAsync(
        Guid userId,
        AdminCompleteNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
    Task<NurseProfileAdminResponse> SetOperationalAccessAsync(
        Guid userId,
        AdminSetNurseOperationalAccessRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

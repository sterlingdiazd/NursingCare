namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed class GetAdminCareRequestsHandler
{
  private readonly IAdminCareRequestRepository _repository;

  public GetAdminCareRequestsHandler(IAdminCareRequestRepository repository)
  {
    _repository = repository;
  }

  public Task<IReadOnlyList<AdminCareRequestListItem>> Handle(
    AdminCareRequestListFilter filter,
    CancellationToken cancellationToken)
  {
    return _repository.GetListAsync(filter, cancellationToken);
  }
}

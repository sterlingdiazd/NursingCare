namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed class GetAdminCareRequestClientOptionsHandler
{
  private readonly IAdminCareRequestRepository _repository;

  public GetAdminCareRequestClientOptionsHandler(IAdminCareRequestRepository repository)
  {
    _repository = repository;
  }

  public Task<IReadOnlyList<AdminCareRequestClientOption>> Handle(
    string? search,
    CancellationToken cancellationToken)
  {
    return _repository.GetActiveClientOptionsAsync(search, cancellationToken);
  }
}

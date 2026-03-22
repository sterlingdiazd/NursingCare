namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed class GetAdminDashboardHandler
{
  private readonly IAdminDashboardRepository _repository;

  public GetAdminDashboardHandler(IAdminDashboardRepository repository)
  {
    _repository = repository;
  }

  public Task<AdminDashboardSnapshot> Handle(CancellationToken cancellationToken)
  {
    return _repository.GetSnapshotAsync(cancellationToken);
  }
}

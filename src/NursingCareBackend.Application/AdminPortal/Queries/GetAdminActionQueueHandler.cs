namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed class GetAdminActionQueueHandler
{
  private readonly IAdminActionQueueRepository _repository;

  public GetAdminActionQueueHandler(IAdminActionQueueRepository repository)
  {
    _repository = repository;
  }

  public Task<IReadOnlyList<AdminActionQueueItem>> Handle(CancellationToken cancellationToken)
  {
    return _repository.GetItemsAsync(cancellationToken);
  }
}

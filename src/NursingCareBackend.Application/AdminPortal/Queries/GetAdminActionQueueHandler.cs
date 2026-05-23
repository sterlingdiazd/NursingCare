namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed class GetAdminActionQueueHandler
{
  private readonly IAdminActionQueueRepository _repository;

  public GetAdminActionQueueHandler(IAdminActionQueueRepository repository)
  {
    _repository = repository;
  }

  public Task<AdminActionQueuePage> Handle(AdminActionQueueFilter filter, CancellationToken cancellationToken)
  {
    return _repository.GetItemsAsync(filter, cancellationToken);
  }
}

namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed class GetAdminClientsHandler
{
  private readonly IAdminClientManagementRepository _repository;

  public GetAdminClientsHandler(IAdminClientManagementRepository repository)
  {
    _repository = repository;
  }

  public Task<IReadOnlyList<AdminClientListItem>> Handle(
    AdminClientListFilter filter,
    CancellationToken cancellationToken)
  {
    return _repository.GetListAsync(filter, cancellationToken);
  }
}

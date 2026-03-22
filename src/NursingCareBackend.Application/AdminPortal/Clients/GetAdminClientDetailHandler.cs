namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed class GetAdminClientDetailHandler
{
  private readonly IAdminClientManagementRepository _repository;

  public GetAdminClientDetailHandler(IAdminClientManagementRepository repository)
  {
    _repository = repository;
  }

  public Task<AdminClientDetail?> Handle(Guid userId, CancellationToken cancellationToken)
  {
    return _repository.GetByIdAsync(userId, cancellationToken);
  }
}

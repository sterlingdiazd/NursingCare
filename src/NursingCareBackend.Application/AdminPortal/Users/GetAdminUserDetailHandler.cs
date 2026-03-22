namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed class GetAdminUserDetailHandler
{
  private readonly IAdminUserManagementRepository _repository;

  public GetAdminUserDetailHandler(IAdminUserManagementRepository repository)
  {
    _repository = repository;
  }

  public Task<AdminUserDetail?> Handle(Guid userId, CancellationToken cancellationToken = default)
    => _repository.GetByIdAsync(userId, cancellationToken);
}

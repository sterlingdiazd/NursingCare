namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed class GetAdminUsersHandler
{
  private readonly IAdminUserManagementRepository _repository;

  public GetAdminUsersHandler(IAdminUserManagementRepository repository)
  {
    _repository = repository;
  }

  public Task<IReadOnlyList<AdminUserListItem>> Handle(
    AdminUserListFilter filter,
    CancellationToken cancellationToken = default)
    => _repository.GetListAsync(filter, cancellationToken);
}

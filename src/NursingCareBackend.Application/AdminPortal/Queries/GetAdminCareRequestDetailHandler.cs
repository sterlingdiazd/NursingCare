namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed class GetAdminCareRequestDetailHandler
{
  private readonly IAdminCareRequestRepository _repository;

  public GetAdminCareRequestDetailHandler(IAdminCareRequestRepository repository)
  {
    _repository = repository;
  }

  public Task<AdminCareRequestDetail?> Handle(Guid careRequestId, CancellationToken cancellationToken)
  {
    return _repository.GetByIdAsync(careRequestId, cancellationToken);
  }
}

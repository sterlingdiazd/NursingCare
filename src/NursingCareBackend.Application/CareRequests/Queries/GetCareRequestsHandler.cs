using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Queries;

public sealed class GetCareRequestsHandler
{
  private readonly ICareRequestRepository _repository;

  public GetCareRequestsHandler(ICareRequestRepository repository)
  {
    _repository = repository;
  }

  public Task<IReadOnlyList<CareRequest>> Handle(CancellationToken cancellationToken)
  {
    return _repository.GetAllAsync(cancellationToken);
  }
}


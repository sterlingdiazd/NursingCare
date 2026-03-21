using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Queries;

public sealed class GetCareRequestByIdHandler
{
  private readonly ICareRequestRepository _repository;

  public GetCareRequestByIdHandler(ICareRequestRepository repository)
  {
    _repository = repository;
  }

  public Task<CareRequest?> Handle(Guid id, Guid? userId, CancellationToken cancellationToken)
  {
    return _repository.GetByIdAsync(id, userId, cancellationToken);
  }
}

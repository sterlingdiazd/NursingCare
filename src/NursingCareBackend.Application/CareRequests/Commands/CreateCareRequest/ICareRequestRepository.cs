using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
public interface ICareRequestRepository
{
  Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken);
}

using NursingCare.Domain.CareRequests;

namespace NursingCare.Application.CareRequests.Commands.CreateCareRequest;
public interface ICareRequestRepository
{
  Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken);
}

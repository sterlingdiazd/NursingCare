using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Application.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
public interface ICareRequestRepository
{
  Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken);
  Task UpdateAsync(CareRequest careRequest, CancellationToken cancellationToken);

  Task<IReadOnlyList<CareRequest>> GetAllAsync(CareRequestAccessScope scope, CancellationToken cancellationToken);

  Task<CareRequest?> GetByIdAsync(Guid id, CareRequestAccessScope scope, CancellationToken cancellationToken);

  Task<int> CountByUserAndUnitTypeAsync(Guid userID, string unitType, CancellationToken cancellationToken);
}

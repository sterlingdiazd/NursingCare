using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
public interface ICareRequestRepository
{
  Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken);
  Task UpdateAsync(CareRequest careRequest, CancellationToken cancellationToken);

  Task<IReadOnlyList<CareRequest>> GetAllAsync(Guid? userId, CancellationToken cancellationToken);

  Task<CareRequest?> GetByIdAsync(Guid id, Guid? userId, CancellationToken cancellationToken);

  Task<int> CountByUserAndUnitTypeAsync(Guid userID, string unitType, CancellationToken cancellationToken);
}

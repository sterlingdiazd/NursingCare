using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
public interface ICareRequestRepository
{
  Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken);
  Task UpdateAsync(CareRequest careRequest, CancellationToken cancellationToken);

  Task<IReadOnlyList<CareRequest>> GetAllAsync(CancellationToken cancellationToken);

  Task<CareRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

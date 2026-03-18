using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.CareRequests;

public sealed class CareRequestRepository : ICareRequestRepository
{
  private readonly NursingCareDbContext _dbContext;

  public CareRequestRepository(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken)
  {
    await _dbContext.CareRequests.AddAsync(careRequest, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task UpdateAsync(CareRequest careRequest, CancellationToken cancellationToken)
  {
    _dbContext.CareRequests.Update(careRequest);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<CareRequest>> GetAllAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.CareRequests
      .OrderByDescending(x => x.CreatedAtUtc)
      .ToListAsync(cancellationToken);
  }

  public Task<CareRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
  {
    return _dbContext.CareRequests
      .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
  }
}

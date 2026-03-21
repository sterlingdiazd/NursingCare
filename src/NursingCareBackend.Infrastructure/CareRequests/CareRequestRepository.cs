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

  public async Task<IReadOnlyList<CareRequest>> GetAllAsync(Guid? userId, CancellationToken cancellationToken)
  {
    var query = _dbContext.CareRequests.AsQueryable();

    if (userId.HasValue)
    {
      query = query.Where(x => x.UserID == userId.Value);
    }

    return await query
      .OrderByDescending(x => x.CreatedAtUtc)
      .ToListAsync(cancellationToken);
  }

  public Task<CareRequest?> GetByIdAsync(Guid id, Guid? userId, CancellationToken cancellationToken)
  {
    var query = _dbContext.CareRequests.Where(x => x.Id == id);

    if (userId.HasValue)
    {
      query = query.Where(x => x.UserID == userId.Value);
    }

    return query.FirstOrDefaultAsync(cancellationToken);
  }

  public Task<int> CountByUserAndUnitTypeAsync(
    Guid userID,
    string unitType,
    CancellationToken cancellationToken)
  {
    return _dbContext.CareRequests
      .CountAsync(x => x.UserID == userID && x.UnitType == unitType, cancellationToken);
  }
}

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests;
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

  public async Task<IReadOnlyList<CareRequest>> GetAllAsync(
    CareRequestAccessScope scope,
    CancellationToken cancellationToken)
  {
    var query = _dbContext.CareRequests.AsQueryable();

    if (scope.CreatedByUserId.HasValue)
    {
      query = query.Where(x => x.UserID == scope.CreatedByUserId.Value);
    }
    else if (scope.AssignedNurseUserId.HasValue)
    {
      query = query.Where(x => x.AssignedNurse == scope.AssignedNurseUserId.Value);
    }

    return await query
      .OrderByDescending(x => x.CreatedAtUtc)
      .ToListAsync(cancellationToken);
  }

  public Task<CareRequest?> GetByIdAsync(
    Guid id,
    CareRequestAccessScope scope,
    CancellationToken cancellationToken)
  {
    var query = _dbContext.CareRequests.Where(x => x.Id == id);

    if (scope.CreatedByUserId.HasValue)
    {
      query = query.Where(x => x.UserID == scope.CreatedByUserId.Value);
    }
    else if (scope.AssignedNurseUserId.HasValue)
    {
      query = query.Where(x => x.AssignedNurse == scope.AssignedNurseUserId.Value);
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

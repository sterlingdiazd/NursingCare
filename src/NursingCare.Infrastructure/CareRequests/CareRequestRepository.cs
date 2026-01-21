using NursingCare.Application.CareRequests.Commands.CreateCareRequest;
using NursingCare.Domain.CareRequests;
using NursingCare.Infrastructure.Persistence;

namespace NursingCare.Infrastructure.CareRequests;

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
}

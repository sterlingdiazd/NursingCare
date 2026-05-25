using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class NursePeriodPaymentRepository : INursePeriodPaymentRepository
{
    private readonly NursingCareDbContext _dbContext;

    public NursePeriodPaymentRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<NursePeriodPayment?> GetAsync(Guid payrollPeriodId, Guid nurseUserId, CancellationToken cancellationToken)
    {
        // Tracked (no AsNoTracking): the idempotent re-confirm path mutates and saves this entity.
        return _dbContext.NursePeriodPayments
            .FirstOrDefaultAsync(
                p => p.PayrollPeriodId == payrollPeriodId && p.NurseUserId == nurseUserId,
                cancellationToken);
    }

    public async Task AddAsync(NursePeriodPayment payment, CancellationToken cancellationToken)
    {
        await _dbContext.NursePeriodPayments.AddAsync(payment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}

using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Persistence for per-(period, nurse) payment confirmations.
/// </summary>
public interface INursePeriodPaymentRepository
{
    /// <summary>Returns the existing confirmation for the (period, nurse) pair, or null.</summary>
    Task<NursePeriodPayment?> GetAsync(Guid payrollPeriodId, Guid nurseUserId, CancellationToken cancellationToken);

    Task AddAsync(NursePeriodPayment payment, CancellationToken cancellationToken);

    /// <summary>Persists pending changes to a tracked confirmation.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

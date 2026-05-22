namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IAdminScheduledDeductionRepository
{
    Task<ScheduledDeductionListResult> GetAsync(Guid? nurseId, string? status, CancellationToken cancellationToken);

    Task<ScheduledDeductionDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(CreateScheduledDeductionRequest request, Guid createdByUserId, CancellationToken cancellationToken);

    /// <summary>Collapse an amortizing plan to a single final installment for the outstanding balance. False when not found.</summary>
    Task<bool> PayoffAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> RescheduleAsync(Guid id, RescheduleScheduledDeductionRequest request, CancellationToken cancellationToken);

    /// <summary>Remove the pending (open-period) installment for the given period. False when not found.</summary>
    Task<bool> SkipInstallmentAsync(Guid id, Guid payrollPeriodId, CancellationToken cancellationToken);

    Task<bool> CancelAsync(Guid id, string reason, Guid cancelledByUserId, CancellationToken cancellationToken);
}

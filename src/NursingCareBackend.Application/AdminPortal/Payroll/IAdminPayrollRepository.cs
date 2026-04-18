namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IAdminPayrollRepository
{
    Task<AdminPayrollPeriodListResult> GetPeriodsAsync(
        AdminPayrollPeriodListFilter filter,
        CancellationToken cancellationToken);

    Task<AdminPayrollPeriodDetail?> GetPeriodByIdAsync(
        Guid periodId,
        CancellationToken cancellationToken);

    Task<Guid> CreatePeriodAsync(
        DateOnly startDate,
        DateOnly endDate,
        DateOnly cutoffDate,
        DateOnly paymentDate,
        CancellationToken cancellationToken);

    Task<bool> ClosePeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken);  // returns false if not found

    Task<IReadOnlyList<AdminPayrollLineItem>> GetPeriodLinesAsync(
        Guid periodId,
        CancellationToken cancellationToken);

    Task<AdminDeductionListResult> GetDeductionsAsync(Guid? nurseId, Guid? periodId, CancellationToken cancellationToken);
    Task<Guid> CreateDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteDeductionAsync(Guid deductionId, CancellationToken cancellationToken);

    Task<AdminCompensationAdjustmentListResult> GetAdjustmentsAsync(Guid? executionId, CancellationToken cancellationToken);
    Task<Guid> CreateAdjustmentAsync(CreateCompensationAdjustmentRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAdjustmentAsync(Guid adjustmentId, CancellationToken cancellationToken);

    // Nurse-scoped queries
    Task<IReadOnlyList<NursePeriodHistoryItem>> GetNursePeriodHistoryAsync(Guid nurseId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<int> CountNurseLinesInOpenPeriodsAsync(Guid nurseId, CancellationToken cancellationToken);
    Task<int> CountNurseLinesInClosedPeriodsAsync(Guid nurseId, CancellationToken cancellationToken);
    Task<NursePeriodDetail?> GetNursePeriodDetailAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken);

    // Voucher data queries
    Task<PayrollVoucherData?> GetVoucherDataAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PayrollVoucherData>> GetAllVoucherDataAsync(Guid periodId, CancellationToken cancellationToken);
}

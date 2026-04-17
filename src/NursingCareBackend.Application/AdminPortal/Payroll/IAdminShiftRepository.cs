namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IAdminShiftRepository
{
    Task<AdminShiftListResult> GetShiftsAsync(AdminShiftListFilter filter, CancellationToken cancellationToken);
    Task<AdminShiftRecordDetail?> GetShiftByIdAsync(Guid shiftId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminShiftChangeItem>> GetShiftChangesAsync(Guid shiftId, CancellationToken cancellationToken);
}
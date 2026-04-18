namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IPayrollVoucherService
{
    Task<byte[]> GenerateVoucherAsync(
        Guid periodId,
        Guid nurseId,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateBulkVouchersZipAsync(
        Guid periodId,
        CancellationToken cancellationToken = default);
}

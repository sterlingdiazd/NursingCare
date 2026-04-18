using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record SubmitOverrideRequest(
    Guid LineId,
    decimal OverrideAmount,
    string Reason
);

public sealed record OverrideDetailDto(
    Guid Id,
    Guid PayrollLineId,
    Guid RequestedByUserId,
    DateTime RequestedAtUtc,
    decimal OverrideAmount,
    string Reason,
    string Status,
    Guid? ApprovedByUserId,
    DateTime? ResolvedAtUtc
);

public interface IAdminPayrollOverrideRepository
{
    Task<Guid> SubmitOverrideAsync(SubmitOverrideRequest request, Guid requestedByUserId, DateTime now, CancellationToken cancellationToken);
    Task<(bool Found, string? Error)> ApproveOverrideAsync(Guid lineId, Guid approvedByUserId, DateTime now, CancellationToken cancellationToken);
    Task<OverrideDetailDto?> GetOverrideForLineAsync(Guid lineId, CancellationToken cancellationToken);
}

namespace NursingCareBackend.Application.AdminPortal.Shifts;

public interface IShiftRecordAdminRepository
{
  Task<Guid> RegisterShiftAsync(
    Guid careRequestId,
    Guid? nurseUserId,
    DateTime? scheduledStartUtc,
    DateTime? scheduledEndUtc,
    CancellationToken cancellationToken);

  Task RecordShiftChangeAsync(
    Guid careRequestId,
    Guid shiftRecordId,
    Guid? newNurseUserId,
    string reason,
    DateTime effectiveAtUtc,
    CancellationToken cancellationToken);
}

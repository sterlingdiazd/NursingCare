namespace NursingCareBackend.Application.AdminPortal.Shifts;

public sealed record RecordCareRequestShiftChangeCommand(
  Guid CareRequestId,
  Guid ShiftRecordId,
  Guid? NewNurseUserId,
  string Reason,
  DateTime? EffectiveAtUtc);

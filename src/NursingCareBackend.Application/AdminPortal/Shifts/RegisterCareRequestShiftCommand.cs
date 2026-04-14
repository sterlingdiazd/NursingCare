namespace NursingCareBackend.Application.AdminPortal.Shifts;

public sealed record RegisterCareRequestShiftCommand(
  Guid CareRequestId,
  Guid? NurseUserId,
  DateTime? ScheduledStartUtc,
  DateTime? ScheduledEndUtc);

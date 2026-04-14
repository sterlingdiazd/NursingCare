namespace NursingCareBackend.Application.AdminPortal.Shifts;

public sealed record AdminShiftRecordSummary(
  Guid Id,
  Guid? NurseUserId,
  string? NurseDisplayName,
  string? NurseEmail,
  DateTime? ScheduledStartUtc,
  DateTime? ScheduledEndUtc,
  DateTime? ActualStartUtc,
  DateTime? ActualEndUtc,
  string Status,
  DateTime CreatedAtUtc,
  DateTime UpdatedAtUtc,
  IReadOnlyList<AdminShiftChangeSummary> Changes);

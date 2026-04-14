namespace NursingCareBackend.Application.AdminPortal.Shifts;

public sealed record AdminShiftChangeSummary(
  Guid Id,
  Guid? PreviousNurseUserId,
  string? PreviousNurseDisplayName,
  string? PreviousNurseEmail,
  Guid? NewNurseUserId,
  string? NewNurseDisplayName,
  string? NewNurseEmail,
  string Reason,
  DateTime EffectiveAtUtc,
  DateTime CreatedAtUtc);

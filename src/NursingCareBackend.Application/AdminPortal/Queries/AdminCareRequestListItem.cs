namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminCareRequestListItem(
  Guid Id,
  Guid ClientUserId,
  string ClientDisplayName,
  string ClientEmail,
  Guid? AssignedNurseUserId,
  string? AssignedNurseDisplayName,
  string? AssignedNurseEmail,
  string CareRequestDescription,
  string CareRequestType,
  int Unit,
  string UnitType,
  decimal Total,
  DateOnly? CareRequestDate,
  string Status,
  DateTime CreatedAtUtc,
  DateTime UpdatedAtUtc,
  DateTime? RejectedAtUtc,
  bool IsOverdueOrStale);

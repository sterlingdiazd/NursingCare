namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed record AdminClientCareRequestHistoryItem(
  Guid CareRequestId,
  string CareRequestDescription,
  string CareRequestType,
  string Status,
  decimal Total,
  DateOnly? CareRequestDate,
  DateTime CreatedAtUtc,
  DateTime UpdatedAtUtc,
  string? AssignedNurseDisplayName,
  string? AssignedNurseEmail);

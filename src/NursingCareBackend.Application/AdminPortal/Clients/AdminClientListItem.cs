namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed record AdminClientListItem(
  Guid UserId,
  string Email,
  string DisplayName,
  string? Name,
  string? LastName,
  string? IdentificationNumber,
  string? Phone,
  bool IsActive,
  int OwnedCareRequestsCount,
  DateTime? LastCareRequestAtUtc,
  DateTime CreatedAtUtc);

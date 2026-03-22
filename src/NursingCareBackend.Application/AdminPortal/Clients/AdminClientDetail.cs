namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed record AdminClientDetail(
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
  bool HasHistoricalCareRequests,
  bool CanAdminCreateCareRequest,
  DateTime CreatedAtUtc,
  IReadOnlyList<AdminClientCareRequestHistoryItem> CareRequestHistory);

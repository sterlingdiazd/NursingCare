namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record AdminUserListItem(
  Guid Id,
  string Email,
  string DisplayName,
  string? Name,
  string? LastName,
  string? IdentificationNumber,
  string? Phone,
  string ProfileType,
  IReadOnlyList<string> RoleNames,
  bool IsActive,
  string AccountStatus,
  bool RequiresProfileCompletion,
  bool RequiresAdminReview,
  bool RequiresManualIntervention,
  DateTime CreatedAtUtc);

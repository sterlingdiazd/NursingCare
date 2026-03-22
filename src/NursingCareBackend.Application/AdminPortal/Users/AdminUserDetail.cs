namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record AdminUserDetail(
  Guid Id,
  string Email,
  string DisplayName,
  string? Name,
  string? LastName,
  string? IdentificationNumber,
  string? Phone,
  string ProfileType,
  IReadOnlyList<string> RoleNames,
  IReadOnlyList<string> AllowedRoleNames,
  bool IsActive,
  string AccountStatus,
  bool RequiresProfileCompletion,
  bool RequiresAdminReview,
  bool RequiresManualIntervention,
  bool HasOperationalHistory,
  int ActiveRefreshTokenCount,
  DateTime CreatedAtUtc,
  AdminUserNurseProfile? NurseProfile,
  AdminUserClientProfile? ClientProfile);

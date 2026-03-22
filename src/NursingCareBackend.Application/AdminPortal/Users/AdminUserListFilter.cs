namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record AdminUserListFilter(
  string? Search,
  string? RoleName,
  string? ProfileType,
  string? Status);

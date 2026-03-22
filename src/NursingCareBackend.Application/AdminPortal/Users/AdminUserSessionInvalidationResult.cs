namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record AdminUserSessionInvalidationResult(
  Guid UserId,
  int RevokedActiveSessionCount);

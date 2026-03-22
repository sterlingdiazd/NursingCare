namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminCareRequestClientOption(
  Guid UserId,
  string DisplayName,
  string Email,
  string? IdentificationNumber);

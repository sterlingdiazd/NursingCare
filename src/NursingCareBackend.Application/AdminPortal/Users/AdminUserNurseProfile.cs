namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record AdminUserNurseProfile(
  bool IsActive,
  DateOnly? HireDate,
  string? Specialty,
  string? LicenseId,
  string? BankName,
  string? AccountNumber,
  string? Category,
  int AssignedCareRequestsCount);

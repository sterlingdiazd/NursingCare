namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record AdminUserIdentityUpdate(
  string Name,
  string LastName,
  string IdentificationNumber,
  string Phone,
  string Email);

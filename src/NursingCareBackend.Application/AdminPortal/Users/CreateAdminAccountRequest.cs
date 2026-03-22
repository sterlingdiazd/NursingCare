namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed record CreateAdminAccountRequest(
  string Name,
  string LastName,
  string IdentificationNumber,
  string Phone,
  string Email,
  string Password,
  string ConfirmPassword);

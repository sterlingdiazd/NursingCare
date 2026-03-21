using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Commands;

public sealed record RegisterRequest(
    string Name,
    string LastName,
    string IdentificationNumber,
    string Phone,
    string Email,
    string Password,
    string ConfirmPassword,
    DateOnly? HireDate = null,
    string? Specialty = null,
    string? LicenseId = null,
    string? BankName = null,
    string? AccountNumber = null,
    UserProfileType ProfileType = UserProfileType.Client
);

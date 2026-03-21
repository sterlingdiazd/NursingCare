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
    UserProfileType ProfileType = UserProfileType.Client
);

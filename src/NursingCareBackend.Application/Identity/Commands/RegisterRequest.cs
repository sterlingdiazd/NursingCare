namespace NursingCareBackend.Application.Identity.Commands;

public enum UserProfileType
{
    Client = 0,
    Nurse = 1
}

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

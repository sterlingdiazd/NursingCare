namespace NursingCareBackend.Application.Identity.Commands;

public enum UserProfileType
{
    Client = 0,
    Nurse = 1
}

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    UserProfileType ProfileType = UserProfileType.Client
);

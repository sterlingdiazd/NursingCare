namespace NursingCareBackend.Application.Identity.Commands;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword
);

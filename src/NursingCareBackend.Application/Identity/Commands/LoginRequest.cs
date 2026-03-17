namespace NursingCareBackend.Application.Identity.Commands;

public sealed record LoginRequest(
    string Email,
    string Password
);

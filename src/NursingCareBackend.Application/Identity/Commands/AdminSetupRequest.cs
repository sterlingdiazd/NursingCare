namespace NursingCareBackend.Application.Identity.Commands;

public sealed record AdminSetupRequest(
    string AdminEmail,
    string AdminPassword
);

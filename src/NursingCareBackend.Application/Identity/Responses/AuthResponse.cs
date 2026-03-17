namespace NursingCareBackend.Application.Identity.Responses;

public sealed record AuthResponse(
    string Token,
    string Email,
    IEnumerable<string> Roles
);

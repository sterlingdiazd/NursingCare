namespace NursingCareBackend.Application.Identity.Responses;

public sealed record AuthResponse(
    string Token,
    string RefreshToken,
    DateTime? ExpiresAtUtc,
    string Email,
    IEnumerable<string> Roles
);

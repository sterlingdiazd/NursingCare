namespace NursingCareBackend.Application.Identity.Responses;

public sealed record AuthResponse(
    string Token,
    string RefreshToken,
    DateTime? ExpiresAtUtc,
    Guid UserId,
    string Email,
    IEnumerable<string> Roles,
    bool RequiresProfileCompletion
);

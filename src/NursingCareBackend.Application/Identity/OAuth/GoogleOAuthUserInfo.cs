namespace NursingCareBackend.Application.Identity.OAuth;

public sealed record GoogleOAuthUserInfo(
    string Subject,
    string Email,
    string? Name,
    bool EmailVerified
);

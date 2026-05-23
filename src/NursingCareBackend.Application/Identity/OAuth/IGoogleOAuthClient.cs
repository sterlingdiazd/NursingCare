namespace NursingCareBackend.Application.Identity.OAuth;

public interface IGoogleOAuthClient
{
    /// <summary>True when all required Google OAuth settings are present in this environment.</summary>
    bool IsConfigured { get; }

    string BuildAuthorizationUrl(string? state = null);
    Task<GoogleOAuthUserInfo> GetUserInfoAsync(string authorizationCode, CancellationToken cancellationToken = default);
}

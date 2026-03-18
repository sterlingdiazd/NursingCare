namespace NursingCareBackend.Application.Identity.OAuth;

public interface IGoogleOAuthClient
{
    string BuildAuthorizationUrl(string? state = null);
    Task<GoogleOAuthUserInfo> GetUserInfoAsync(string authorizationCode, CancellationToken cancellationToken = default);
}

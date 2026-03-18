namespace NursingCareBackend.Application.Identity.OAuth;

public interface IGoogleOAuthClient
{
    string BuildAuthorizationUrl();
    Task<GoogleOAuthUserInfo> GetUserInfoAsync(string authorizationCode, CancellationToken cancellationToken = default);
}

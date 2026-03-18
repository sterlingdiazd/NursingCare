using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Identity.OAuth;

namespace NursingCareBackend.Infrastructure.Authentication;

public sealed class GoogleOAuthClient : IGoogleOAuthClient
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";

    private readonly HttpClient _httpClient;
    private readonly GoogleOAuthOptions _options;

    public GoogleOAuthClient(HttpClient httpClient, IOptions<GoogleOAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string BuildAuthorizationUrl(string? state = null)
    {
        EnsureConfigured();

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["access_type"] = "online",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "select_account"
        };

        if (!string.IsNullOrWhiteSpace(state))
        {
            parameters["state"] = state;
        }

        var query = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

        return $"{AuthorizationEndpoint}?{query}";
    }

    public async Task<GoogleOAuthUserInfo> GetUserInfoAsync(
        string authorizationCode,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var tokenResponse = await ExchangeCodeAsync(authorizationCode, cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Google did not return an access token.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Google user info request failed: {payload}");
        }

        var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfoResponse>(cancellationToken: cancellationToken);
        if (userInfo is null || string.IsNullOrWhiteSpace(userInfo.Sub) || string.IsNullOrWhiteSpace(userInfo.Email))
        {
            throw new InvalidOperationException("Google user info response was incomplete.");
        }

        return new GoogleOAuthUserInfo(
            Subject: userInfo.Sub,
            Email: userInfo.Email,
            Name: userInfo.Name,
            EmailVerified: userInfo.EmailVerified);
    }

    private async Task<GoogleTokenResponse> ExchangeCodeAsync(
        string authorizationCode,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = authorizationCode,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Google token exchange failed: {payload}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
        if (tokenResponse is null)
        {
            throw new InvalidOperationException("Google token response was empty.");
        }

        return tokenResponse;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || string.IsNullOrWhiteSpace(_options.RedirectUri)
            || string.IsNullOrWhiteSpace(_options.FrontendRedirectUrl))
        {
            throw new InvalidOperationException(
                "Google OAuth is not configured. Set GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET, GOOGLE_OAUTH_REDIRECT_URI, and GOOGLE_OAUTH_FRONTEND_REDIRECT_URL.");
        }
    }

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record GoogleUserInfoResponse(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email_verified")] bool EmailVerified);
}

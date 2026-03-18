namespace NursingCareBackend.Infrastructure.Authentication;

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "GoogleOAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string FrontendRedirectUrl { get; set; } = string.Empty;
    public string MobileRedirectUrl { get; set; } = string.Empty;
}

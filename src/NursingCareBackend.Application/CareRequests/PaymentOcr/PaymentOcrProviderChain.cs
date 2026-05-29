namespace NursingCareBackend.Application.CareRequests.PaymentOcr;

/// <summary>
/// Pure resolution of the ordered provider chain to attempt, given the
/// configured <c>Provider</c> mode and which providers actually have
/// credentials. Kept separate from the orchestrator so the precedence rules
/// are unit-testable without any HTTP wiring.
/// </summary>
public static class PaymentOcrProviderChain
{
    public const string Azure = "AzureVision";
    public const string GoogleVision = "GoogleVision";
    public const string OcrSpace = "OcrSpace";

    /// <summary>
    /// Fixed priority when more than one provider is eligible: Azure first (most
    /// reliable primary), then Google Vision (high 20 MB / 75 MP image limit),
    /// then OCR.space (deep free reserve, 25k req/month).
    /// </summary>
    private static readonly string[] Priority = [Azure, GoogleVision, OcrSpace];

    /// <summary>
    /// Returns the provider names to try, in order. Empty when OCR is disabled
    /// or nothing is configured.
    /// </summary>
    /// <param name="providerMode">Disabled | Auto | AzureVision | OcrSpace (case-insensitive).</param>
    /// <param name="configuredProviders">Provider names whose credentials are present.</param>
    public static IReadOnlyList<string> Resolve(
        string? providerMode,
        ISet<string> configuredProviders)
    {
        var mode = (providerMode ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(mode) || mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // Explicit single-provider modes: honor only if that provider is configured.
        foreach (var name in Priority)
        {
            if (mode.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return configuredProviders.Contains(name) ? [name] : [];
            }
        }

        // "Auto" (and any other non-Disabled value): every configured provider,
        // in fixed priority order, so the primary is tried before the fallback.
        return Priority.Where(configuredProviders.Contains).ToArray();
    }
}

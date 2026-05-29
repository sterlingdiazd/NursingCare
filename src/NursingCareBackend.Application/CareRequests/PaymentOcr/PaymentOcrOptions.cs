namespace NursingCareBackend.Application.CareRequests.PaymentOcr;

/// <summary>
/// Configuration for automatic payment-proof reading. The reader is a stacked
/// chain: a primary provider with an optional free fallback so a single
/// provider outage never disables the feature.
/// <list type="bullet">
///   <item><c>Disabled</c> - OCR off entirely; the client always types the data (demo-safe default).</item>
///   <item><c>Auto</c> - try every configured provider in priority order (AzureVision, then OcrSpace).</item>
///   <item><c>AzureVision</c> - Azure AI Vision only.</item>
///   <item><c>OcrSpace</c> - the free OCR.space API only.</item>
/// </list>
/// A provider only participates when its own credentials are present, so
/// leaving a key blank cleanly removes it from the chain.
/// </summary>
public sealed class PaymentOcrOptions
{
    public const string SectionName = "PaymentOcr";

    /// <summary>
    /// <c>Auto</c> (default) runs the whole chain - Azure primary, then the
    /// configured fallbacks - so a single provider outage is invisible to the
    /// user. Only providers with credentials participate, so the chain is empty
    /// (graceful manual entry) until at least one key is set.
    /// </summary>
    public string Provider { get; set; } = "Auto";

    /// <summary>Per-provider HTTP ceiling. Bounds the worst-case spinner the client sees.</summary>
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>Primary, most-reliable provider.</summary>
    public string AzureVisionEndpoint { get; set; } = string.Empty;
    public string AzureVisionKey { get; set; } = string.Empty;

    /// <summary>High-resolution fallback (20 MB / 75 MP). Get a key in Google Cloud (Vision API).</summary>
    public string GoogleVisionEndpoint { get; set; } = "https://vision.googleapis.com/v1/images:annotate";
    public string GoogleVisionApiKey { get; set; } = string.Empty;

    /// <summary>Deep free reserve. Get a key at https://ocr.space/ocrapi (25k req/month free tier).</summary>
    public string OcrSpaceEndpoint { get; set; } = "https://api.ocr.space/parse/image";
    public string OcrSpaceApiKey { get; set; } = string.Empty;
}

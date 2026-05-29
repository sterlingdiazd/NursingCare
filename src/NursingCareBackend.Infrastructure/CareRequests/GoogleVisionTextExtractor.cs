using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.CareRequests.PaymentOcr;

namespace NursingCareBackend.Infrastructure.CareRequests;

/// <summary>
/// Reads payment-proof text with Google Cloud Vision's <c>images:annotate</c>
/// (DOCUMENT_TEXT_DETECTION). Accepts images up to 20 MB / 75 MP - far above
/// OCR.space's free-tier cap - so it is the high-resolution fallback in the
/// chain. Resilient by contract: any failure or self-imposed timeout returns
/// <c>null</c> so the orchestrator falls through; only a genuine caller
/// cancellation propagates.
/// </summary>
public sealed class GoogleVisionTextExtractor : IPaymentProofTextExtractor
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<PaymentOcrOptions> _options;
    private readonly ILogger<GoogleVisionTextExtractor> _logger;

    public GoogleVisionTextExtractor(
        HttpClient httpClient,
        IOptions<PaymentOcrOptions> options,
        ILogger<GoogleVisionTextExtractor> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string ProviderName => PaymentOcrProviderChain.GoogleVision;

    private string Endpoint
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("PAYMENT_OCR_GOOGLE_VISION_ENDPOINT")
                ?? _options.Value.GoogleVisionEndpoint;
            return string.IsNullOrWhiteSpace(configured)
                ? "https://vision.googleapis.com/v1/images:annotate"
                : configured;
        }
    }

    private string ApiKey =>
        Environment.GetEnvironmentVariable("PAYMENT_OCR_GOOGLE_VISION_KEY")
            ?? _options.Value.GoogleVisionApiKey;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task<string?> ExtractTextAsync(PaymentProofOcrInput input, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            var payload = new
            {
                requests = new[]
                {
                    new
                    {
                        image = new { content = Convert.ToBase64String(input.ImageContent) },
                        features = new[] { new { type = "DOCUMENT_TEXT_DETECTION" } },
                        imageContext = new { languageHints = new[] { "es" } },
                    },
                },
            };

            var uri = $"{Endpoint}?key={Uri.EscapeDataString(ApiKey)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ExtractText(document.RootElement, input.CareRequestId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Vision OCR failed for care request {CareRequestId}.", input.CareRequestId);
            return null;
        }
    }

    private string? ExtractText(JsonElement root, Guid careRequestId)
    {
        if (!root.TryGetProperty("responses", out var responses) ||
            responses.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var response in responses.EnumerateArray())
        {
            if (response.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(message.GetString()))
            {
                _logger.LogWarning(
                    "Google Vision reported an error for care request {CareRequestId}: {Error}",
                    careRequestId,
                    message.GetString());
                return null;
            }

            if (response.TryGetProperty("fullTextAnnotation", out var fullText) &&
                fullText.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
            {
                return text.GetString();
            }

            // Sparse-text fallback: the first textAnnotations entry holds the whole block.
            if (response.TryGetProperty("textAnnotations", out var annotations) &&
                annotations.ValueKind == JsonValueKind.Array)
            {
                foreach (var annotation in annotations.EnumerateArray())
                {
                    if (annotation.TryGetProperty("description", out var description) &&
                        description.ValueKind == JsonValueKind.String)
                    {
                        return description.GetString();
                    }
                }
            }

            // First response processed; no text found.
            return string.Empty;
        }

        return string.Empty;
    }
}

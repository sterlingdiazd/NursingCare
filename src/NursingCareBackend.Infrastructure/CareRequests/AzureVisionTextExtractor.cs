using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.CareRequests.PaymentOcr;

namespace NursingCareBackend.Infrastructure.CareRequests;

/// <summary>
/// Reads payment-proof text with Azure AI Vision's Image Analysis (read)
/// feature. Resilient by contract: any transport error or self-imposed timeout
/// returns <c>null</c> so the orchestrator can fall through to the next
/// provider. Only a genuine caller cancellation propagates.
/// </summary>
public sealed class AzureVisionTextExtractor : IPaymentProofTextExtractor
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<PaymentOcrOptions> _options;
    private readonly ILogger<AzureVisionTextExtractor> _logger;

    public AzureVisionTextExtractor(
        HttpClient httpClient,
        IOptions<PaymentOcrOptions> options,
        ILogger<AzureVisionTextExtractor> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string ProviderName => PaymentOcrProviderChain.Azure;

    private string Endpoint =>
        Environment.GetEnvironmentVariable("PAYMENT_OCR_AZURE_VISION_ENDPOINT")
            ?? _options.Value.AzureVisionEndpoint;

    private string Key =>
        Environment.GetEnvironmentVariable("PAYMENT_OCR_AZURE_VISION_KEY")
            ?? _options.Value.AzureVisionKey;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Key);

    public async Task<string?> ExtractTextAsync(PaymentProofOcrInput input, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            var uri = $"{Endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=read";
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Add("Ocp-Apim-Subscription-Key", Key);
            request.Content = new ByteArrayContent(input.ImageContent);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(input.ContentType);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ExtractReadText(document.RootElement);
        }
        // A genuine caller cancellation propagates; a self-imposed HttpClient
        // timeout (token not signalled by the caller) is just a provider miss.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Vision OCR failed for care request {CareRequestId}.", input.CareRequestId);
            return null;
        }
    }

    private static string ExtractReadText(JsonElement root)
    {
        if (!root.TryGetProperty("readResult", out var readResult) ||
            !readResult.TryGetProperty("blocks", out var blocks) ||
            blocks.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var block in blocks.EnumerateArray())
        {
            if (!block.TryGetProperty("lines", out var blockLines) ||
                blockLines.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var line in blockLines.EnumerateArray())
            {
                if (line.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    lines.Add(text.GetString()!);
                }
            }
        }

        return string.Join("\n", lines);
    }
}

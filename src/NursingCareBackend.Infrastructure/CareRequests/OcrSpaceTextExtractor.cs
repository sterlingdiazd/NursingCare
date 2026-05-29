using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.CareRequests.PaymentOcr;

namespace NursingCareBackend.Infrastructure.CareRequests;

/// <summary>
/// Free fallback reader backed by the OCR.space API (https://ocr.space/ocrapi).
/// The free tier gives 25k requests/month with a 1 MB image limit, so it is a
/// reasonable safety net when Azure AI Vision is unavailable - or the only
/// provider when no Azure key is configured. Resilient by contract: any
/// failure or self-imposed timeout returns <c>null</c>; only a genuine caller
/// cancellation propagates.
/// </summary>
public sealed class OcrSpaceTextExtractor : IPaymentProofTextExtractor
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<PaymentOcrOptions> _options;
    private readonly ILogger<OcrSpaceTextExtractor> _logger;

    public OcrSpaceTextExtractor(
        HttpClient httpClient,
        IOptions<PaymentOcrOptions> options,
        ILogger<OcrSpaceTextExtractor> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string ProviderName => PaymentOcrProviderChain.OcrSpace;

    private string Endpoint
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("PAYMENT_OCR_OCRSPACE_ENDPOINT")
                ?? _options.Value.OcrSpaceEndpoint;
            return string.IsNullOrWhiteSpace(configured) ? "https://api.ocr.space/parse/image" : configured;
        }
    }

    private string ApiKey =>
        Environment.GetEnvironmentVariable("PAYMENT_OCR_OCRSPACE_KEY")
            ?? _options.Value.OcrSpaceApiKey;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task<string?> ExtractTextAsync(PaymentProofOcrInput input, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("apikey", ApiKey);

            var fileContent = new ByteArrayContent(input.ImageContent);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(input.ContentType);

            // OCREngine 2 handles Latin-script receipts best and auto-detects language;
            // scale upsizes low-res phone photos. "file" is the documented upload field.
            var form = new MultipartFormDataContent
            {
                { fileContent, "file", FileNameFor(input.ContentType) },
                { new StringContent("2"), "OCREngine" },
                { new StringContent("true"), "scale" },
                { new StringContent("true"), "isTable" },
            };
            request.Content = form;

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ExtractParsedText(document.RootElement, input.CareRequestId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR.space OCR failed for care request {CareRequestId}.", input.CareRequestId);
            return null;
        }
    }

    private string? ExtractParsedText(JsonElement root, Guid careRequestId)
    {
        if (root.TryGetProperty("IsErroredOnProcessing", out var errored) &&
            errored.ValueKind == JsonValueKind.True)
        {
            _logger.LogWarning(
                "OCR.space reported a processing error for care request {CareRequestId}: {Error}",
                careRequestId,
                ReadErrorMessage(root));
            return null;
        }

        // OCRExitCode: 1 success, 2 partial success - both yield usable text.
        if (root.TryGetProperty("OCRExitCode", out var exitCode) &&
            exitCode.TryGetInt32(out var code) &&
            code is not (1 or 2))
        {
            _logger.LogWarning(
                "OCR.space returned exit code {Code} for care request {CareRequestId}: {Error}",
                code,
                careRequestId,
                ReadErrorMessage(root));
            return null;
        }

        if (!root.TryGetProperty("ParsedResults", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var result in results.EnumerateArray())
        {
            if (result.TryGetProperty("ParsedText", out var parsedText) &&
                parsedText.ValueKind == JsonValueKind.String)
            {
                var value = parsedText.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    builder.Append(value);
                }
            }
        }

        return builder.ToString();
    }

    private static string ReadErrorMessage(JsonElement root)
    {
        if (!root.TryGetProperty("ErrorMessage", out var error))
        {
            return "(sin detalle)";
        }

        // ErrorMessage is a string or an array of strings depending on the failure.
        return error.ValueKind switch
        {
            JsonValueKind.String => error.GetString() ?? "(sin detalle)",
            JsonValueKind.Array => string.Join("; ", error.EnumerateArray().Select(e => e.GetString())),
            _ => "(sin detalle)"
        };
    }

    private static string FileNameFor(string contentType)
    {
        var ext = contentType switch
        {
            "image/png" => "png",
            "image/webp" => "webp",
            "application/pdf" => "pdf",
            _ => "jpg"
        };
        return $"comprobante.{ext}";
    }
}

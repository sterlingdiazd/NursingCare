using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.CareRequests.PaymentOcr;

namespace NursingCareBackend.Infrastructure.CareRequests;

/// <summary>
/// Orchestrates the payment-proof OCR chain: it resolves the configured
/// provider order (primary + free fallback), tries each configured extractor
/// in turn, and parses the first non-empty read into an assessment. When OCR
/// is disabled, unconfigured, or every provider misses, it returns a graceful
/// degraded assessment (HTTP 200) that tells the client to type the data by
/// hand - never an error - so a provider outage can never block reporting a
/// payment.
/// </summary>
public sealed class PaymentProofOcrService : IPaymentProofOcrService
{
    private readonly IReadOnlyDictionary<string, IPaymentProofTextExtractor> _extractors;
    private readonly IOptions<PaymentOcrOptions> _options;
    private readonly ILogger<PaymentProofOcrService> _logger;

    public PaymentProofOcrService(
        IEnumerable<IPaymentProofTextExtractor> extractors,
        IOptions<PaymentOcrOptions> options,
        ILogger<PaymentProofOcrService> logger)
    {
        _extractors = extractors.ToDictionary(e => e.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options = options;
        _logger = logger;
    }

    public async Task<PaymentOcrAssessment> AssessAsync(
        PaymentProofOcrInput input,
        CancellationToken cancellationToken)
    {
        var configured = _extractors.Values
            .Where(e => e.IsConfigured)
            .Select(e => e.ProviderName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var chain = PaymentOcrProviderChain.Resolve(_options.Value.Provider, configured);

        if (chain.Count == 0)
        {
            return PaymentProofTextParser.BuildManualEntry("Disabled");
        }

        for (var i = 0; i < chain.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var providerName = chain[i];
            if (!_extractors.TryGetValue(providerName, out var extractor))
            {
                continue;
            }

            var text = await extractor.ExtractTextAsync(input, cancellationToken);
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Success - the client receives the values transparently. We note
                // which provider answered (it may be a fallback) for diagnostics.
                if (i > 0)
                {
                    _logger.LogInformation(
                        "OCR provider {Provider} read the proof for care request {CareRequestId} after {Failed} failed.",
                        extractor.ProviderName,
                        input.CareRequestId,
                        string.Join(", ", chain.Take(i)));
                }
                return PaymentProofTextParser.Build(text, extractor.ProviderName, input.InvoiceTotal, []);
            }

            // The service that failed is logged, then the next one is tried silently.
            var hasNext = i < chain.Count - 1;
            _logger.LogWarning(
                "OCR provider {Provider} returned no text for care request {CareRequestId}.{NextHint}",
                providerName,
                input.CareRequestId,
                hasNext ? $" Falling back to {chain[i + 1]}." : string.Empty);
        }

        _logger.LogWarning(
            "All OCR providers ({Chain}) failed for care request {CareRequestId}; degrading to manual entry.",
            string.Join(", ", chain),
            input.CareRequestId);

        return PaymentProofTextParser.BuildManualEntry(chain[^1]);
    }
}

namespace NursingCareBackend.Application.CareRequests.PaymentOcr;

/// <summary>
/// A single OCR backend that turns a payment-proof image into raw text. One
/// implementation per provider (Azure AI Vision, OCR.space). Implementations
/// MUST be resilient: any transport failure or self-imposed timeout returns
/// <c>null</c> so the orchestrator can fall through to the next provider and,
/// ultimately, degrade to manual entry. Only a genuine caller cancellation
/// (the request token) should surface as an <see cref="OperationCanceledException"/>.
/// </summary>
public interface IPaymentProofTextExtractor
{
    /// <summary>Stable provider label recorded on the resulting assessment (e.g. "AzureVision").</summary>
    string ProviderName { get; }

    /// <summary>True when the provider's credentials are present, so it can join the chain.</summary>
    bool IsConfigured { get; }

    /// <summary>Returns the extracted text, or <c>null</c>/empty when this provider could not read it.</summary>
    Task<string?> ExtractTextAsync(PaymentProofOcrInput input, CancellationToken cancellationToken);
}

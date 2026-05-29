namespace NursingCareBackend.Application.CareRequests.PaymentOcr;

public sealed record PaymentOcrAssessment(
    string DraftSentence,
    string? ExtractedBankReference,
    decimal? ExtractedAmount,
    DateOnly? ExtractedPaymentDate,
    string? ExtractedBank,
    decimal Confidence,
    IReadOnlyList<string> Warnings,
    string Provider,
    DateTime AssessedAtUtc);

public sealed record PaymentProofOcrInput(
    Guid CareRequestId,
    byte[] ImageContent,
    string ContentType,
    decimal InvoiceTotal);

public interface IPaymentProofOcrService
{
    Task<PaymentOcrAssessment> AssessAsync(
        PaymentProofOcrInput input,
        CancellationToken cancellationToken);
}

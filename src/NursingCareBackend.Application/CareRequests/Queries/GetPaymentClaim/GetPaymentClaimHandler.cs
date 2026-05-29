using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;

namespace NursingCareBackend.Application.CareRequests.Queries.GetPaymentClaim;

/// <summary>
/// The structured payment claim the client reported, plus anti-fraud flags, for the admin to review
/// BEFORE confirming. The image itself is fetched separately (GET /payment-proof); this is the data
/// the admin matches against their bank. Returns null when the care request does not exist.
/// </summary>
public sealed record PaymentClaimReview(
    bool HasProof,
    string? ClaimedBankReference,
    decimal? ClaimedAmount,
    DateOnly? ClaimedPaymentDate,
    string? PayingBank,
    string? Note,
    DateTime? UploadedAtUtc,
    decimal InvoiceTotal,
    // Anti-fraud flags the admin should heed before confirming.
    bool AmountReported,
    bool AmountMatches,
    bool ReusedReference,
    string? OcrDraftSentence,
    string? OcrExtractedBankReference,
    decimal? OcrExtractedAmount,
    DateOnly? OcrExtractedPaymentDate,
    string? OcrExtractedBank,
    decimal? OcrConfidence,
    string? OcrWarningsJson,
    string? OcrProvider,
    DateTime? OcrAssessedAtUtc,
    bool OcrClientEdited
);

public sealed class GetPaymentClaimHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IPaymentProofRepository _proofs;
    private readonly IPaymentValidationRepository _validations;

    public GetPaymentClaimHandler(
        ICareRequestRepository repository,
        IPaymentProofRepository proofs,
        IPaymentValidationRepository validations)
    {
        _repository = repository;
        _proofs = proofs;
        _validations = validations;
    }

    public async Task<PaymentClaimReview?> Handle(Guid careRequestId, CancellationToken cancellationToken)
    {
        var careRequest = await _repository.GetByIdAsync(
            careRequestId, CareRequestAccessScope.Admin, cancellationToken);
        if (careRequest is null)
        {
            return null;
        }

        var proof = await _proofs.GetLatestByCareRequestIdAsync(careRequestId, cancellationToken);
        if (proof is null)
        {
            return new PaymentClaimReview(
                HasProof: false,
                ClaimedBankReference: null,
                ClaimedAmount: null,
                ClaimedPaymentDate: null,
                PayingBank: null,
                Note: null,
                UploadedAtUtc: null,
                InvoiceTotal: careRequest.Total,
                AmountReported: false,
                AmountMatches: false,
                ReusedReference: false,
                OcrDraftSentence: null,
                OcrExtractedBankReference: null,
                OcrExtractedAmount: null,
                OcrExtractedPaymentDate: null,
                OcrExtractedBank: null,
                OcrConfidence: null,
                OcrWarningsJson: null,
                OcrProvider: null,
                OcrAssessedAtUtc: null,
                OcrClientEdited: false);
        }

        var reusedReference = !string.IsNullOrWhiteSpace(proof.ClaimedBankReference)
            && await _validations.IsBankReferenceUsedAsync(
                proof.ClaimedBankReference!, careRequest.Id, cancellationToken);

        return new PaymentClaimReview(
            HasProof: true,
            ClaimedBankReference: proof.ClaimedBankReference,
            ClaimedAmount: proof.ClaimedAmount,
            ClaimedPaymentDate: proof.ClaimedPaymentDate,
            PayingBank: proof.PayingBank,
            Note: proof.Note,
            UploadedAtUtc: proof.UploadedAtUtc,
            InvoiceTotal: careRequest.Total,
            AmountReported: proof.ClaimedAmount.HasValue,
            AmountMatches: proof.AmountMatches(careRequest.Total),
            ReusedReference: reusedReference,
            OcrDraftSentence: proof.OcrDraftSentence,
            OcrExtractedBankReference: proof.OcrExtractedBankReference,
            OcrExtractedAmount: proof.OcrExtractedAmount,
            OcrExtractedPaymentDate: proof.OcrExtractedPaymentDate,
            OcrExtractedBank: proof.OcrExtractedBank,
            OcrConfidence: proof.OcrConfidence,
            OcrWarningsJson: proof.OcrWarningsJson,
            OcrProvider: proof.OcrProvider,
            OcrAssessedAtUtc: proof.OcrAssessedAtUtc,
            OcrClientEdited: proof.OcrClientEdited);
    }
}

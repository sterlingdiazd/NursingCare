namespace NursingCareBackend.Application.CareRequests.Commands.ReportPayment;

/// <summary>A client reports a payment by uploading a proof image (invoice photo / transfer screenshot)
/// plus the structured claim around it (bank reference, amount, date, paying bank) for the admin to
/// match against the bank. The claim fields are optional for back-compat but enable anti-fraud checks.</summary>
public sealed record ReportPaymentCommand(
    Guid CareRequestId,
    Guid ActingUserId,
    byte[] ImageContent,
    string ContentType,
    string? Note,
    string? ClaimedBankReference = null,
    decimal? ClaimedAmount = null,
    DateOnly? ClaimedPaymentDate = null,
    string? PayingBank = null,
    string? OcrDraftSentence = null,
    string? OcrExtractedBankReference = null,
    decimal? OcrExtractedAmount = null,
    DateOnly? OcrExtractedPaymentDate = null,
    string? OcrExtractedBank = null,
    decimal? OcrConfidence = null,
    string? OcrWarningsJson = null,
    string? OcrProvider = null,
    DateTime? OcrAssessedAtUtc = null,
    bool OcrClientEdited = false);

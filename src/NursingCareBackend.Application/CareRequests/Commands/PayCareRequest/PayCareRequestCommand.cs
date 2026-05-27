namespace NursingCareBackend.Application.CareRequests.Commands.PayCareRequest;

public sealed record PayCareRequestCommand(
    Guid CareRequestId,
    string BankReference,
    DateTime PaymentDate,
    Guid ActingAdminUserId,
    // Anti-fraud: confirming with a bank reference already used on another request is blocked unless
    // the admin explicitly acknowledges it (e.g. a bank that genuinely reuses references).
    bool AcknowledgeDuplicateReference = false
);

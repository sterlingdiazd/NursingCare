namespace NursingCareBackend.Application.CareRequests.Commands.IssueCreditNote;

/// <summary>Admin records a credit note / refund against a Paid care request. <see cref="Reference"/>
/// is the optional external ref (bank transfer of the refund, manual credit-note number).</summary>
public sealed record IssueCreditNoteCommand(
    Guid CareRequestId,
    decimal Amount,
    string Reason,
    string? Reference,
    Guid ActingAdminUserId
);

public sealed record IssueCreditNoteResponse(
    Guid Id,
    Guid CareRequestId,
    decimal Amount,
    string Reason,
    string? Reference,
    DateTime IssuedAtUtc,
    decimal TotalCredited
);

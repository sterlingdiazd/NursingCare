namespace NursingCareBackend.Application.CareRequests.Commands.RejectPaymentProof;

/// <summary>
/// Admin rejects a client's reported payment proof (with a required reason). The care request
/// returns to Invoiced, the proof is cleared, the reason is recorded, and the client is notified to
/// re-report. Audited. No revenue is recognized.
/// </summary>
public sealed record RejectPaymentProofCommand(
    Guid CareRequestId,
    Guid ActingAdminUserId,
    string Reason);

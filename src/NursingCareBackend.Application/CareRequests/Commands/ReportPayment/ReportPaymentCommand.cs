namespace NursingCareBackend.Application.CareRequests.Commands.ReportPayment;

/// <summary>A client reports a payment by uploading a proof image (invoice photo / transfer screenshot).</summary>
public sealed record ReportPaymentCommand(
    Guid CareRequestId,
    Guid ActingUserId,
    byte[] ImageContent,
    string ContentType,
    string? Note);

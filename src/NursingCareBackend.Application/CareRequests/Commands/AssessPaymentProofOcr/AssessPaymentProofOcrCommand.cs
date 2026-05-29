namespace NursingCareBackend.Application.CareRequests.Commands.AssessPaymentProofOcr;

public sealed record AssessPaymentProofOcrCommand(
    Guid CareRequestId,
    Guid ActingUserId,
    byte[] ImageContent,
    string ContentType);

namespace NursingCareBackend.Application.CareRequests.Commands.VoidCareRequest;

public sealed record VoidCareRequestCommand(
    Guid CareRequestId,
    string VoidReason,
    Guid ActingAdminUserId,
    DateTime VoidedAtUtc
);

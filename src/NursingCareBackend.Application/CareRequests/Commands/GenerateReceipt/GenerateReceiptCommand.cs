namespace NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;

public sealed record GenerateReceiptCommand(
    Guid CareRequestId,
    Guid ActingAdminUserId
);

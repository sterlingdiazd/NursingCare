namespace NursingCareBackend.Application.CareRequests.Commands.PayCareRequest;

public sealed record PayCareRequestCommand(
    Guid CareRequestId,
    string BankReference,
    DateTime PaymentDate,
    Guid ActingAdminUserId
);

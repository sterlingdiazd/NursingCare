namespace NursingCareBackend.Application.CareRequests.Commands.InvoiceCareRequest;

public sealed record InvoiceCareRequestCommand(
    Guid CareRequestId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    Guid ActingAdminUserId
);

namespace NursingCareBackend.Application.CareRequests;

public sealed record ReceiptPdfData(
    Guid CareRequestId,
    string ReceiptNumber,
    string ClientDisplayName,
    string? ClientIdentificationNumber,
    string CareRequestType,
    int Unit,
    string UnitType,
    decimal Total,
    string InvoiceNumber,
    DateTime InvoicedAtUtc,
    DateTime PaidAtUtc,
    string BankReference,
    DateTime GeneratedAtUtc
);

public interface IReceiptPdfService
{
    byte[] Generate(ReceiptPdfData data);
}

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
    DateTime GeneratedAtUtc,
    string CompanyName = "NursingCare",
    // Dominican fiscal comprobante (e-NCF). When non-null, the document is a formal fiscal receipt
    // and shows the NCF + issuer RNC + ITBIS + legal footer. When null, it is a non-fiscal proforma
    // / cuenta de cobro labelled with the SOL- InvoiceNumber.
    string? Ncf = null,
    DateTime? NcfIssuedAtUtc = null
);

public interface IReceiptPdfService
{
    /// <summary>Renders the receipt/comprobante PDF. Async because the issuer fiscal config
    /// (RNC, ITBIS rate, legal footer, currency) is resolved live from the owner-editable
    /// SystemSettings (FISCAL_*) via <c>IFiscalSettingsProvider</c>, so edits apply without a redeploy.</summary>
    Task<byte[]> GenerateAsync(ReceiptPdfData data, CancellationToken cancellationToken = default);
}

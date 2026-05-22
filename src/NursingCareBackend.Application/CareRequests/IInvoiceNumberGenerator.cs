namespace NursingCareBackend.Application.CareRequests;

/// <summary>
/// Produces the next invoice number for a care request. Scheme is configurable (informal
/// SOL-yyyyMM-#### by default; e-CF NcfType+10 digits when enabled).
/// </summary>
public interface IInvoiceNumberGenerator
{
    Task<string> NextAsync(DateTime invoiceDateUtc, CancellationToken cancellationToken);
}

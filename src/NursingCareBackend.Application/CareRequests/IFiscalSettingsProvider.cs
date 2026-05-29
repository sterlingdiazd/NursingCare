namespace NursingCareBackend.Application.CareRequests;

/// <summary>
/// Effective fiscal/invoicing values for Dominican comprobantes and receipts. Editable by the
/// owner via SystemSettings (FISCAL_*), so changes take effect without a redeploy.
/// </summary>
public sealed record FiscalSettings(
    string? Rnc,
    decimal ItbisRatePercent,
    bool NcfEnabled,
    string NcfType,
    string InvoiceNumberPrefix,
    string CurrencyCode,
    string? LegalFooter);

public interface IFiscalSettingsProvider
{
    Task<FiscalSettings> GetAsync(CancellationToken cancellationToken = default);
}

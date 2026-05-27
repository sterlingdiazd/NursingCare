namespace NursingCareBackend.Application.CareRequests;

/// <summary>
/// Produces document numbers for a care request, split by fiscal weight so a DGII e-NCF
/// sequence is only ever consumed when a fiscal receipt is actually issued:
/// <list type="bullet">
///   <item><see cref="NextProformaAsync"/> — always a NON-fiscal proforma / cuenta de cobro
///   ({Prefix}-yyyyMM-####, e.g. SOL-202605-0001). Assigned on completion regardless of fiscal mode.</item>
///   <item><see cref="NextFiscalNcfAsync"/> — the formal e-NCF ({NcfType}{##########}, e.g. E320000000001).
///   Only called when payment is confirmed AND <c>FiscalOptions.NcfEnabled</c> is true, so the
///   fiscal sequence is never burned on completions or voids (which would leave sequence gaps).</item>
/// </list>
/// The two counters are independent: proformas count by <c>InvoicedAtUtc</c>; e-NCFs count by
/// <c>NcfIssuedAtUtc</c>.
/// </summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>True when the business runs in DGII fiscal mode (FISCAL_NCF_ENABLED), meaning a
    /// formal e-NCF must be issued at payment confirmation. Resolved live from the owner-editable
    /// SystemSettings via <c>IFiscalSettingsProvider</c> (async because it reads the DB), so toggling
    /// the setting takes effect without a redeploy. The Application layer reads this through the
    /// generator so it never has to depend on Infrastructure's FiscalOptions.</summary>
    Task<bool> IsFiscalModeEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>Next NON-fiscal proforma number ({Prefix}-yyyyMM-####), counted by month over
    /// <c>InvoicedAtUtc</c>. Always this scheme — never an e-NCF.</summary>
    Task<string> NextProformaAsync(DateTime invoiceDateUtc, CancellationToken cancellationToken);

    /// <summary>Next formal e-NCF ({NcfType}{##########}), counted over the independent
    /// <c>NcfIssuedAtUtc</c> sequence. Caller must only invoke this when fiscal mode is enabled.</summary>
    Task<string> NextFiscalNcfAsync(DateTime issuedAtUtc, CancellationToken cancellationToken);
}

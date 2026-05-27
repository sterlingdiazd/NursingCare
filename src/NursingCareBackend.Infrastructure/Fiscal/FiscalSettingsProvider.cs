using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Fiscal;

/// <summary>
/// Reads fiscal/invoicing config from the editable SystemSettings (FISCAL_*) at runtime, falling
/// back to the appsettings <see cref="FiscalOptions"/> defaults when a key is missing or blank.
/// This makes the owner-editable values flow dynamically into invoice numbering and receipt PDFs
/// without a redeploy. Read fresh per call (mirrors CompanyInfoProvider) so edits are live.
/// </summary>
public sealed class FiscalSettingsProvider : IFiscalSettingsProvider
{
    private readonly NursingCareDbContext _db;
    private readonly FiscalOptions _fallback;

    public FiscalSettingsProvider(NursingCareDbContext db, IOptions<FiscalOptions> fallback)
    {
        _db = db;
        _fallback = fallback.Value;
    }

    public async Task<FiscalSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var keys = new[]
        {
            "FISCAL_RNC", "FISCAL_ITBIS_RATE_PERCENT", "FISCAL_NCF_ENABLED", "FISCAL_NCF_TYPE",
            "FISCAL_INVOICE_PREFIX", "FISCAL_CURRENCY", "FISCAL_LEGAL_FOOTER",
        };
        var values = await _db.SystemSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string? Val(string key) =>
            values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        // RNC and LegalFooter are intentionally optional: a blank setting means "informal / none",
        // so we surface the trimmed setting value (or null) without forcing the appsettings fallback.
        var rnc = values.TryGetValue("FISCAL_RNC", out var rncRaw)
            ? (string.IsNullOrWhiteSpace(rncRaw) ? null : rncRaw.Trim())
            : _fallback.Rnc;
        var legalFooter = values.TryGetValue("FISCAL_LEGAL_FOOTER", out var footerRaw)
            ? (string.IsNullOrWhiteSpace(footerRaw) ? null : footerRaw.Trim())
            : _fallback.LegalFooter;

        var itbis = _fallback.ItbisRatePercent;
        if (Val("FISCAL_ITBIS_RATE_PERCENT") is { } itbisRaw
            && decimal.TryParse(itbisRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedItbis))
        {
            itbis = parsedItbis;
        }

        var ncfEnabled = _fallback.NcfEnabled;
        if (Val("FISCAL_NCF_ENABLED") is { } ncfRaw && bool.TryParse(ncfRaw, out var parsedNcf))
        {
            ncfEnabled = parsedNcf;
        }

        var ncfType = Val("FISCAL_NCF_TYPE") ?? _fallback.NcfType;
        var invoicePrefix = Val("FISCAL_INVOICE_PREFIX") ?? _fallback.InvoiceNumberPrefix;
        var currency = Val("FISCAL_CURRENCY") ?? _fallback.CurrencyCode;

        return new FiscalSettings(rnc, itbis, ncfEnabled, ncfType, invoicePrefix, currency, legalFooter);
    }
}

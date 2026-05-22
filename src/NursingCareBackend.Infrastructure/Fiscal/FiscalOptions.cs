namespace NursingCareBackend.Infrastructure.Fiscal;

/// <summary>
/// Configurable fiscal/format behavior for Dominican invoices, receipts and reports.
/// Defaults model a small INFORMAL business (no RNC/NCF, ITBIS exempt). Flipping these
/// (via the "Fiscal" config section) upgrades toward e-CF without code changes.
/// </summary>
public sealed class FiscalOptions
{
    public const string SectionName = "Fiscal";

    /// <summary>Issuer RNC (tax id). Blank = informal (no fiscal credit).</summary>
    public string? Rnc { get; set; }

    /// <summary>ITBIS rate as a percent. 0 = exempt (health services are typically exempt).</summary>
    public decimal ItbisRatePercent { get; set; } = 0m;

    /// <summary>When true, invoice numbers follow the e-CF scheme (NcfType + 10 digits).</summary>
    public bool NcfEnabled { get; set; } = false;

    /// <summary>e-CF document type: E31 (B2B credit) or E32 (B2C consumer).</summary>
    public string NcfType { get; set; } = "E32";

    /// <summary>Prefix for the informal (non-fiscal) invoice number scheme: {Prefix}-{yyyyMM}-{####}.</summary>
    public string InvoiceNumberPrefix { get; set; } = "SOL";

    public string CurrencyCode { get; set; } = "DOP";
    public string Locale { get; set; } = "es-DO";

    /// <summary>Optional legal footer printed on documents.</summary>
    public string? LegalFooter { get; set; }
}

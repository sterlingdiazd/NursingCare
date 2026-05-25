namespace NursingCareBackend.Application.AdminPortal.Payroll.Validation;

/// <summary>
/// Transport-agnostic reconciliation data for a financial document. Carries the text the document
/// must surface (company, title, recipient, period) and the monetary components needed to verify
/// that totals reconcile. Money stays <see cref="decimal"/> end to end.
///
/// For a payroll voucher the components are the per-line sums:
/// <c>Net == Base + TransportIncentive + ComplexityBonus + MedicalSuppliesCompensation + Adjustments − Deductions</c>.
/// </summary>
public sealed class FinancialDocumentData
{
    /// <summary>Company name that MUST appear on the document (from CompanyInfoOptions/SystemSettings).</summary>
    public required string CompanyName { get; init; }

    /// <summary>Document title that MUST appear on the document (e.g. "COMPROBANTE DE PAGO").</summary>
    public required string DocumentTitle { get; init; }

    /// <summary>Recipient/subject name (e.g. the nurse) that MUST appear and be non-empty.</summary>
    public required string RecipientName { get; init; }

    /// <summary>Human period label (e.g. "01/04/2026 al 30/04/2026") that MUST be non-empty.</summary>
    public required string PeriodLabel { get; init; }

    public decimal BaseCompensation { get; init; }
    public decimal TransportIncentive { get; init; }
    public decimal ComplexityBonus { get; init; }
    public decimal MedicalSuppliesCompensation { get; init; }
    public decimal Adjustments { get; init; }
    public decimal Deductions { get; init; }

    /// <summary>The net amount printed on the document; must reconcile with the components above.</summary>
    public decimal NetCompensation { get; init; }

    /// <summary>
    /// Builds reconciliation data for a payroll voucher from the existing <see cref="PayrollVoucherData"/>.
    /// Component sums are taken from the line items so the validator reconciles against the same numbers
    /// the repository used to derive the printed totals.
    /// </summary>
    public static FinancialDocumentData ForPayrollVoucher(
        PayrollVoucherData voucher,
        string companyName,
        string documentTitle,
        string periodLabel)
    {
        ArgumentNullException.ThrowIfNull(voucher);

        return new FinancialDocumentData
        {
            CompanyName = companyName,
            DocumentTitle = documentTitle,
            RecipientName = voucher.NurseDisplayName,
            PeriodLabel = periodLabel,
            BaseCompensation = voucher.Lines.Sum(l => l.BaseCompensation),
            TransportIncentive = voucher.Lines.Sum(l => l.TransportIncentive),
            ComplexityBonus = voucher.Lines.Sum(l => l.ComplexityBonus),
            MedicalSuppliesCompensation = voucher.Lines.Sum(l => l.MedicalSuppliesCompensation),
            Adjustments = voucher.TotalAdjustments,
            Deductions = voucher.TotalDeductions,
            NetCompensation = voucher.NetCompensation,
        };
    }
}

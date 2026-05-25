namespace NursingCareBackend.Application.AdminPortal.Payroll.Validation;

/// <summary>
/// Pre-send validation gate for financial documents (PDFs). Every financial document
/// MUST pass this gate before it is emailed or downloaded; a failure BLOCKS delivery and
/// carries a precise Spanish reason so the action can be retried.
///
/// The validator is deliberately transport-agnostic and stateless: callers generate the PDF
/// and the reconciliation data, then ask the validator to confirm both render integrity and
/// accounting integrity. It never throws for document corruption — corruption is reported as a
/// failed <see cref="FinancialValidationResult"/> so the caller can convert it into a blocked
/// delivery (fail-closed) without unwinding the surrounding operation.
/// </summary>
public interface IFinancialOutputValidator
{
    /// <summary>
    /// Validates a generated financial document against its source data.
    /// </summary>
    /// <param name="kind">Which financial document is being validated.</param>
    /// <param name="pdfBytes">The rendered PDF bytes (may be null/empty for a corrupt render).</param>
    /// <param name="data">The reconciliation data the document was built from.</param>
    /// <returns>
    /// A result that is valid only when every render and accounting rule passes. On failure the
    /// result lists each failure (stable code + Spanish message) and a Spanish summary.
    /// </returns>
    FinancialValidationResult Validate(
        FinancialDocumentKind kind,
        byte[]? pdfBytes,
        FinancialDocumentData data);
}

/// <summary>Financial document families that can be gated. Only <see cref="PayrollVoucher"/> is implemented today.</summary>
public enum FinancialDocumentKind
{
    PayrollVoucher,
    PayrollPeriodReport,
    Receipt,
}

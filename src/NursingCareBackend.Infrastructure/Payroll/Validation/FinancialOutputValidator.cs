using System.Globalization;
using System.Text;
using NursingCareBackend.Application.AdminPortal.Payroll.Validation;
using UglyToad.PdfPig;

namespace NursingCareBackend.Infrastructure.Payroll.Validation;

/// <summary>
/// Validates a generated financial PDF before it is emailed or downloaded. Two rule layers run:
///
///   1. Render/format integrity — the bytes are a readable PDF that contains the required tokens
///      (company name, document title, period label, "Total", a currency marker) and the line that
///      carries the total is not clipped (it still contains a digit).
///   2. Accounting/economic integrity — the printed net reconciles with its components within
///      ±0.01, required text fields are present, and the net is not negative.
///
/// The validator is fail-closed for true corruption (null/empty/non-PDF bytes) but never throws:
/// every problem is surfaced as a <see cref="FinancialValidationResult"/> failure so the caller can
/// block delivery cleanly. PDF text is extracted in-process with PdfPig (no shelling out to
/// pdftotext, which is not portable to production).
/// </summary>
public sealed class FinancialOutputValidator : IFinancialOutputValidator
{
    // Render thresholds.
    private const int MinPdfBytes = 1024; // ~1KB: anything smaller cannot be a real voucher.
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private const string PdfEofMarker = "%%EOF";

    // Accounting tolerance: monetary reconciliation is exact to the cent.
    private const decimal MoneyTolerance = 0.01m;

    // Failure codes (stable; the mobile client and tests depend on these literals).
    public const string CodeRenderEmpty = "RENDER_EMPTY";
    public const string CodeRenderNotPdf = "RENDER_NOT_PDF";
    public const string CodeRenderTruncated = "RENDER_TRUNCATED";
    public const string CodeRenderUnreadable = "RENDER_UNREADABLE";
    public const string CodeMissingToken = "MISSING_TOKEN";
    public const string CodeTotalClipped = "TOTAL_CLIPPED";
    public const string CodeTotalsMismatch = "TOTALS_MISMATCH";
    public const string CodeNegativeNet = "NEGATIVE_NET";
    public const string CodeMissingField = "MISSING_FIELD";

    public FinancialValidationResult Validate(
        FinancialDocumentKind kind,
        byte[]? pdfBytes,
        FinancialDocumentData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return kind switch
        {
            FinancialDocumentKind.PayrollVoucher => ValidatePayrollVoucher(pdfBytes, data),

            // TODO extend: PayrollPeriodReport and Receipt reuse the same two rule layers; wire their
            // required tokens / reconciliation shape here when those documents are gated.
            FinancialDocumentKind.PayrollPeriodReport => throw new NotSupportedException(
                "La validación del reporte de período aún no está implementada."),
            FinancialDocumentKind.Receipt => throw new NotSupportedException(
                "La validación del recibo aún no está implementada."),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de documento financiero desconocido."),
        };
    }

    private static FinancialValidationResult ValidatePayrollVoucher(byte[]? pdfBytes, FinancialDocumentData data)
    {
        var failures = new List<FinancialValidationFailure>();

        // --- Layer 1: render/format integrity -----------------------------------------------
        // Byte-level checks gate text extraction: if the bytes are not a plausible PDF, stop here
        // (fail-closed) rather than attempting to read tokens out of garbage.
        if (!TryValidatePdfBytes(pdfBytes, failures, out var pdfText))
        {
            return FinancialValidationResult.Failure(failures);
        }

        ValidateRequiredTokens(pdfText, data, failures);
        ValidateTotalNotClipped(pdfText, failures);

        // --- Layer 2: accounting/economic integrity -----------------------------------------
        ValidateRequiredFields(data, failures);
        ValidateReconciliation(data, failures);

        return failures.Count == 0
            ? FinancialValidationResult.Success("El comprobante superó la validación financiera.")
            : FinancialValidationResult.Failure(failures);
    }

    /// <summary>
    /// Byte-level + readability checks. Returns false (and leaves <paramref name="pdfText"/> empty)
    /// when the document is too corrupt to inspect further.
    /// </summary>
    private static bool TryValidatePdfBytes(
        byte[]? pdfBytes,
        List<FinancialValidationFailure> failures,
        out string pdfText)
    {
        pdfText = string.Empty;

        if (pdfBytes is null || pdfBytes.Length == 0)
        {
            failures.Add(new FinancialValidationFailure(
                CodeRenderEmpty,
                "El documento generado está vacío; no se puede enviar."));
            return false;
        }

        if (pdfBytes.Length < MinPdfBytes)
        {
            failures.Add(new FinancialValidationFailure(
                CodeRenderTruncated,
                "El documento generado es demasiado pequeño y parece estar incompleto."));
            return false;
        }

        if (!StartsWith(pdfBytes, PdfMagic))
        {
            failures.Add(new FinancialValidationFailure(
                CodeRenderNotPdf,
                "El archivo generado no es un PDF válido."));
            return false;
        }

        if (!ContainsAscii(pdfBytes, PdfEofMarker))
        {
            failures.Add(new FinancialValidationFailure(
                CodeRenderTruncated,
                "El PDF generado está truncado (falta el marcador de fin de archivo)."));
            return false;
        }

        try
        {
            pdfText = ExtractText(pdfBytes);
        }
        catch (Exception)
        {
            // A throw during extraction is itself a render failure — the bytes claim to be a PDF
            // but cannot be read, so we must not ship it.
            failures.Add(new FinancialValidationFailure(
                CodeRenderUnreadable,
                "No se pudo leer el contenido del PDF generado; el documento está dañado."));
            return false;
        }

        return true;
    }

    private static void ValidateRequiredTokens(
        string pdfText,
        FinancialDocumentData data,
        List<FinancialValidationFailure> failures)
    {
        // Required tokens (case-insensitive): company name, document title, period label,
        // a "Total" word, and a currency marker.
        AssertTokenPresent(pdfText, data.CompanyName, "el nombre de la empresa", failures);
        AssertTokenPresent(pdfText, data.DocumentTitle, "el título del documento", failures);
        AssertTokenPresent(pdfText, data.PeriodLabel, "el período", failures);
        AssertTokenPresent(pdfText, "Total", "el resumen de total", failures);

        var hasCurrencyMarker = ContainsIgnoreCase(pdfText, "RD$") || ContainsIgnoreCase(pdfText, "DOP");
        if (!hasCurrencyMarker)
        {
            failures.Add(new FinancialValidationFailure(
                CodeMissingToken,
                "El comprobante no muestra la moneda (RD$ o DOP)."));
        }
    }

    /// <summary>
    /// Clipping heuristic: the line that contains "Total" must also contain a digit, otherwise the
    /// amount was cut off at the page margin.
    /// </summary>
    private static void ValidateTotalNotClipped(string pdfText, List<FinancialValidationFailure> failures)
    {
        var totalLine = pdfText
            .Split('\n')
            .FirstOrDefault(line => line.Contains("Total", StringComparison.OrdinalIgnoreCase));

        // If "Total" is missing entirely the MISSING_TOKEN check already fired; only flag clipping
        // when the word is present but its amount digit is not.
        if (totalLine is not null && !totalLine.Any(char.IsDigit))
        {
            failures.Add(new FinancialValidationFailure(
                CodeTotalClipped,
                "El monto total parece estar recortado en el documento."));
        }
    }

    private static void ValidateRequiredFields(FinancialDocumentData data, List<FinancialValidationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(data.CompanyName))
        {
            failures.Add(new FinancialValidationFailure(
                CodeMissingField, "Falta el nombre de la empresa en los datos del comprobante."));
        }

        if (string.IsNullOrWhiteSpace(data.RecipientName))
        {
            failures.Add(new FinancialValidationFailure(
                CodeMissingField, "Falta el nombre de la enfermera en los datos del comprobante."));
        }

        if (string.IsNullOrWhiteSpace(data.PeriodLabel))
        {
            failures.Add(new FinancialValidationFailure(
                CodeMissingField, "Falta el período en los datos del comprobante."));
        }
    }

    private static void ValidateReconciliation(FinancialDocumentData data, List<FinancialValidationFailure> failures)
    {
        if (data.NetCompensation < 0m)
        {
            failures.Add(new FinancialValidationFailure(
                CodeNegativeNet,
                $"El neto a pagar es negativo ({FormatCurrency(data.NetCompensation)}); no se puede emitir el comprobante."));
        }

        var expectedNet =
            data.BaseCompensation
            + data.TransportIncentive
            + data.ComplexityBonus
            + data.MedicalSuppliesCompensation
            + data.Adjustments
            - data.Deductions;

        if (Math.Abs(expectedNet - data.NetCompensation) > MoneyTolerance)
        {
            failures.Add(new FinancialValidationFailure(
                CodeTotalsMismatch,
                $"Los totales no cuadran: el neto del comprobante ({FormatCurrency(data.NetCompensation)}) "
                + $"no coincide con la suma de sus componentes ({FormatCurrency(expectedNet)})."));
        }
    }

    private static void AssertTokenPresent(
        string pdfText,
        string token,
        string label,
        List<FinancialValidationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(token) || !ContainsIgnoreCase(pdfText, token.Trim()))
        {
            failures.Add(new FinancialValidationFailure(
                CodeMissingToken,
                $"El comprobante no contiene {label}."));
        }
    }

    private static string ExtractText(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsAscii(byte[] bytes, string marker)
    {
        var markerBytes = Encoding.ASCII.GetBytes(marker);
        if (markerBytes.Length == 0 || bytes.Length < markerBytes.Length)
        {
            return false;
        }

        for (var i = 0; i <= bytes.Length - markerBytes.Length; i++)
        {
            var match = true;
            for (var j = 0; j < markerBytes.Length; j++)
            {
                if (bytes[i + j] != markerBytes[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsIgnoreCase(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string FormatCurrency(decimal value) =>
        $"RD$ {value.ToString("N2", CultureInfo.GetCultureInfo("es-DO"))}";
}

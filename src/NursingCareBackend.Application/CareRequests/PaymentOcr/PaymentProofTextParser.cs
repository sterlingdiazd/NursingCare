using System.Globalization;
using System.Text.RegularExpressions;

namespace NursingCareBackend.Application.CareRequests.PaymentOcr;

/// <summary>
/// Provider-agnostic parsing of raw OCR text into a structured
/// <see cref="PaymentOcrAssessment"/>. Pure (no I/O), so every OCR backend
/// produces identical fields and warnings and the logic is unit-testable.
/// The output is always a non-binding draft - the admin still verifies the
/// money in the bank before confirming.
/// </summary>
public static class PaymentProofTextParser
{
    private static readonly Regex AmountRegex = new(
        @"(?i)(?:RD\s*\$|DOP|RD\$)\s*(?<amount>\d{1,3}(?:[,.]\d{3})*(?:[,.]\d{2})|\d+(?:[,.]\d{2})?)",
        RegexOptions.Compiled);

    private static readonly Regex ReferenceRegex = new(
        @"(?i)(?:referencia|ref(?:erencia)?|no\.?|numero|n[uú]mero|transacci[oó]n|autorizaci[oó]n)\s*[:#-]?\s*(?<ref>[A-Z0-9][A-Z0-9-]{3,})",
        RegexOptions.Compiled);

    private static readonly Regex DateRegex = new(
        @"(?<date>\b\d{4}[-/]\d{1,2}[-/]\d{1,2}\b|\b\d{1,2}[-/]\d{1,2}[-/]\d{2,4}\b)",
        RegexOptions.Compiled);

    private static readonly (string Token, string Display)[] KnownBanks =
    [
        ("banreservas", "Banreservas"),
        ("reservas", "Banreservas"),
        ("popular", "Banco Popular"),
        ("bhd", "BHD"),
        ("scotiabank", "Scotiabank"),
        ("santa cruz", "Banco Santa Cruz"),
        ("vimenca", "Vimenca"),
        ("adopem", "Banco Adopem")
    ];

    /// <summary>
    /// A neutral, non-failure assessment for the rare case where no provider in
    /// the chain could read the proof (or OCR is off). The chain exists so this
    /// is virtually never reached, so it must not look like an error: it simply
    /// invites manual entry without "we couldn't read it" language or the
    /// per-field "not identified" warnings.
    /// </summary>
    public static PaymentOcrAssessment BuildManualEntry(string provider)
        => new(
            "Ingresa los datos del pago del comprobante. Sol y Luna los verificará en el banco antes de confirmar.",
            ExtractedBankReference: null,
            ExtractedAmount: null,
            ExtractedPaymentDate: null,
            ExtractedBank: null,
            Confidence: 0m,
            Warnings: [],
            Provider: provider,
            AssessedAtUtc: DateTime.UtcNow);

    public static PaymentOcrAssessment Build(
        string? text,
        string provider,
        decimal invoiceTotal,
        IReadOnlyList<string> initialWarnings)
    {
        var safeText = text ?? string.Empty;
        var warnings = new List<string>(initialWarnings);
        var amount = TryExtractAmount(safeText);
        var reference = TryExtractReference(safeText);
        var date = TryExtractDate(safeText);
        var bank = TryExtractBank(safeText);

        if (string.IsNullOrWhiteSpace(safeText))
        {
            warnings.Add("La app no leyó texto suficiente del comprobante.");
        }
        if (!amount.HasValue)
        {
            warnings.Add("La app no identificó un monto.");
        }
        else if (amount.Value != invoiceTotal)
        {
            warnings.Add("El monto leído no coincide con el total facturado.");
        }
        if (string.IsNullOrWhiteSpace(reference))
        {
            warnings.Add("La app no identificó una referencia bancaria.");
        }

        var signals = 0;
        if (amount.HasValue) signals++;
        if (!string.IsNullOrWhiteSpace(reference)) signals++;
        if (date.HasValue) signals++;
        if (!string.IsNullOrWhiteSpace(bank)) signals++;
        var confidence = signals == 0 ? 0m : Math.Round(signals / 4m, 2);

        return new PaymentOcrAssessment(
            BuildDraftSentence(amount, reference, date, bank, invoiceTotal),
            reference,
            amount,
            date,
            bank,
            confidence,
            warnings.Distinct().ToArray(),
            provider,
            DateTime.UtcNow);
    }

    private static string BuildDraftSentence(
        decimal? amount,
        string? reference,
        DateOnly? date,
        string? bank,
        decimal invoiceTotal)
    {
        if (!amount.HasValue && string.IsNullOrWhiteSpace(reference))
        {
            return "Borrador OCR: la app no pudo validar datos suficientes del comprobante. Requiere confirmación bancaria.";
        }

        var parts = new List<string>();
        if (amount.HasValue)
        {
            parts.Add($"un pago de {Money(amount.Value)}");
        }
        if (!string.IsNullOrWhiteSpace(reference))
        {
            parts.Add($"referencia {reference}");
        }
        if (!string.IsNullOrWhiteSpace(bank))
        {
            parts.Add($"banco {bank}");
        }
        if (date.HasValue)
        {
            parts.Add($"fecha {date.Value:dd/MM/yyyy}");
        }

        var match = amount.HasValue && amount.Value == invoiceTotal
            ? " El monto leído coincide con la factura."
            : amount.HasValue
                ? " El monto leído no coincide con el total facturado."
                : string.Empty;

        return $"Borrador OCR: la app leyó {string.Join(", ", parts)}.{match} Requiere confirmación bancaria.";
    }

    private static decimal? TryExtractAmount(string text)
    {
        var match = AmountRegex.Match(text);
        if (!match.Success) return null;
        return ParseLooseDecimal(match.Groups["amount"].Value);
    }

    private static string? TryExtractReference(string text)
    {
        var match = ReferenceRegex.Match(text);
        return match.Success ? match.Groups["ref"].Value.Trim().ToUpperInvariant() : null;
    }

    private static DateOnly? TryExtractDate(string text)
    {
        var match = DateRegex.Match(text);
        if (!match.Success) return null;

        var value = match.Groups["date"].Value;
        string[] formats =
        [
            "yyyy-M-d",
            "yyyy-MM-dd",
            "yyyy/M/d",
            "yyyy/MM/dd",
            "d-M-yyyy",
            "dd-MM-yyyy",
            "d/M/yyyy",
            "dd/MM/yyyy",
            "d-M-yy",
            "dd-MM-yy",
            "d/M/yy",
            "dd/MM/yy"
        ];

        return DateOnly.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? TryExtractBank(string text)
    {
        var normalized = text.ToLowerInvariant();
        foreach (var (token, display) in KnownBanks)
        {
            if (normalized.Contains(token, StringComparison.Ordinal))
            {
                return display;
            }
        }

        return null;
    }

    private static decimal? ParseLooseDecimal(string raw)
    {
        var clean = raw.Trim().Replace(" ", string.Empty);
        var lastComma = clean.LastIndexOf(',');
        var lastDot = clean.LastIndexOf('.');
        var decimalIndex = Math.Max(lastComma, lastDot);

        string normalized;
        if (decimalIndex >= 0 && clean.Length - decimalIndex - 1 <= 2)
        {
            var integerPart = new string(clean[..decimalIndex].Where(char.IsDigit).ToArray());
            var decimalPart = new string(clean[(decimalIndex + 1)..].Where(char.IsDigit).ToArray());
            normalized = $"{integerPart}.{decimalPart}";
        }
        else
        {
            normalized = new string(clean.Where(char.IsDigit).ToArray());
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
    }

    private static string Money(decimal value)
        => $"RD${value.ToString("N2", CultureInfo.GetCultureInfo("es-DO"))}";
}

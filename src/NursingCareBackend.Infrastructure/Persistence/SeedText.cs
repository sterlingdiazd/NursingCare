namespace NursingCareBackend.Infrastructure.Persistence;

/// <summary>
/// Human-readable Spanish labels for service-type codes, used by the seeders so seeded
/// descriptions never expose raw codes (e.g. "domicilio_24h") or English text. Mirrors the
/// catalog DisplayNames; falls back to a humanized form for any unmapped code.
/// </summary>
internal static class SeedText
{
    private static readonly Dictionary<string, string> ServiceLabels = new()
    {
        { "hogar_diario",        "Cuidado diario en el hogar" },
        { "hogar_basico",        "Cuidado básico en el hogar" },
        { "hogar_estandar",      "Cuidado estándar en el hogar" },
        { "hogar_premium",       "Cuidado premium en el hogar" },
        { "domicilio_dia_12h",   "Atención domiciliaria diurna (12 horas)" },
        { "domicilio_noche_12h", "Atención domiciliaria nocturna (12 horas)" },
        { "domicilio_24h",       "Atención domiciliaria 24 horas" },
        { "suero",               "Administración de sueros" },
        { "medicamentos",        "Administración de medicamentos" },
        { "sonda_vesical",       "Colocación de sonda vesical" },
        { "sonda_nasogastrica",  "Colocación de sonda nasogástrica" },
        { "sonda_peg",           "Manejo de sonda PEG" },
        { "curas",               "Curaciones de heridas" },
    };

    /// <summary>Spanish label for a service-type code, or a humanized fallback.</summary>
    public static string Label(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Servicio de enfermería";
        if (ServiceLabels.TryGetValue(code, out var label)) return label;
        // Fallback: "domicilio_dia_12h" -> "Domicilio dia 12h"
        var humanized = code.Replace('_', ' ').Trim();
        return humanized.Length == 0 ? "Servicio de enfermería" : char.ToUpperInvariant(humanized[0]) + humanized[1..];
    }
}

namespace NursingCareBackend.Application.Identity.Validation;

public static class NurseProfileCatalog
{
    public static readonly string[] Specialties =
    [
        "Cuidado de adultos",
        "Cuidado pediatrico",
        "Cuidado geriatrico",
        "Cuidados intensivos",
        "Atencion domiciliaria",
    ];

    public static readonly string[] Categories =
    [
        "Junior",
        "Semisenior",
        "Senior",
        "Lider",
    ];

    private static readonly IReadOnlyDictionary<string, string> SpecialtyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Adult Care"] = "Cuidado de adultos",
            ["Cuidado de adultos"] = "Cuidado de adultos",
            ["Pediatric Care"] = "Cuidado pediatrico",
            ["Cuidado pediatrico"] = "Cuidado pediatrico",
            ["Geriatric Care"] = "Cuidado geriatrico",
            ["Cuidado geriatrico"] = "Cuidado geriatrico",
            ["Critical Care"] = "Cuidados intensivos",
            ["Cuidados intensivos"] = "Cuidados intensivos",
            ["Home Care"] = "Atencion domiciliaria",
            ["Atencion domiciliaria"] = "Atencion domiciliaria",
        };

    private static readonly IReadOnlyDictionary<string, string> CategoryAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Junior"] = "Junior",
            ["Semi Senior"] = "Semisenior",
            ["Semisenior"] = "Semisenior",
            ["Senior"] = "Senior",
            ["Lead"] = "Lider",
            ["Lider"] = "Lider",
        };

    public static string? NormalizeSpecialty(string? value)
        => NormalizeValue(value, SpecialtyAliases);

    public static string? NormalizeCategory(string? value)
        => NormalizeValue(value, CategoryAliases);

    public static string NormalizeRequiredSpecialty(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Specialty is required.", parameterName);
        }

        if (!SpecialtyAliases.TryGetValue(value.Trim(), out var normalized))
        {
            throw new ArgumentException("Specialty is not valid.", parameterName);
        }

        return normalized;
    }

    public static string NormalizeRequiredCategory(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Category is required.", parameterName);
        }

        if (!CategoryAliases.TryGetValue(value.Trim(), out var normalized))
        {
            throw new ArgumentException("Category is not valid.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeValue(string? value, IReadOnlyDictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return aliases.TryGetValue(value.Trim(), out var normalized)
            ? normalized
            : value.Trim();
    }
}

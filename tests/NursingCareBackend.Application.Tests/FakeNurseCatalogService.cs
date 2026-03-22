using NursingCareBackend.Application.Catalogs;

namespace NursingCareBackend.Application.Tests;

internal sealed class FakeNurseCatalogService : INurseCatalogService
{
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

    public Task<string> NormalizeRequiredSpecialtyAsync(string? value, string parameterName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Specialty is required.", parameterName);
        }

        var trimmed = value.Trim();
        if (!SpecialtyAliases.TryGetValue(trimmed, out var normalized))
        {
            throw new ArgumentException("Specialty is not valid.", parameterName);
        }

        return Task.FromResult(normalized);
    }

    public Task<string> NormalizeRequiredCategoryAsync(string? value, string parameterName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Category is required.", parameterName);
        }

        var trimmed = value.Trim();
        if (!CategoryAliases.TryGetValue(trimmed, out var normalized))
        {
            throw new ArgumentException("Category is not valid.", parameterName);
        }

        return Task.FromResult(normalized);
    }

    public Task<string?> NormalizeSpecialtyAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult<string?>(null);
        }

        var trimmed = value.Trim();
        return Task.FromResult<string?>(
            SpecialtyAliases.TryGetValue(trimmed, out var sn) ? sn : trimmed);
    }

    public Task<string?> NormalizeCategoryAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult<string?>(null);
        }

        var trimmed = value.Trim();
        return Task.FromResult<string?>(
            CategoryAliases.TryGetValue(trimmed, out var cn) ? cn : trimmed);
    }

    public Task<IReadOnlyList<NurseCatalogOption>> GetActiveSpecialtiesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<NurseCatalogOption>>(Array.Empty<NurseCatalogOption>());

    public Task<IReadOnlyList<NurseCatalogOption>> GetActiveCategoriesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<NurseCatalogOption>>(Array.Empty<NurseCatalogOption>());
}

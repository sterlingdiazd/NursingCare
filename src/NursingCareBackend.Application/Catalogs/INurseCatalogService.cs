namespace NursingCareBackend.Application.Catalogs;

public interface INurseCatalogService
{
    Task<string> NormalizeRequiredSpecialtyAsync(string? value, string parameterName, CancellationToken cancellationToken);

    Task<string> NormalizeRequiredCategoryAsync(string? value, string parameterName, CancellationToken cancellationToken);

    Task<string?> NormalizeSpecialtyAsync(string? value, CancellationToken cancellationToken);

    Task<string?> NormalizeCategoryAsync(string? value, CancellationToken cancellationToken);

    Task<IReadOnlyList<NurseCatalogOption>> GetActiveSpecialtiesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NurseCatalogOption>> GetActiveCategoriesAsync(CancellationToken cancellationToken);
}

public sealed record NurseCatalogOption(string Code, string DisplayName);

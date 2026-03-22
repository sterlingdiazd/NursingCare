using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Domain.Catalogs;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Catalogs;

public sealed class NurseCatalogService : INurseCatalogService
{
    private readonly NursingCareDbContext _dbContext;
    private IReadOnlyList<NurseSpecialtyCatalog>? _specialties;
    private IReadOnlyList<NurseCategoryCatalog>? _categories;

    public NurseCatalogService(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> NormalizeRequiredSpecialtyAsync(
        string? value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Specialty is required.", parameterName);
        }

        await EnsureLoadedAsync(cancellationToken);
        var resolved = ResolveSpecialty(value);
        if (resolved is null)
        {
            throw new ArgumentException("Specialty is not valid.", parameterName);
        }

        return resolved;
    }

    public async Task<string> NormalizeRequiredCategoryAsync(
        string? value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Category is required.", parameterName);
        }

        await EnsureLoadedAsync(cancellationToken);
        var resolved = ResolveCategory(value);
        if (resolved is null)
        {
            throw new ArgumentException("Category is not valid.", parameterName);
        }

        return resolved;
    }

    public async Task<string?> NormalizeSpecialtyAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        await EnsureLoadedAsync(cancellationToken);
        return ResolveSpecialty(value) ?? value.Trim();
    }

    public async Task<string?> NormalizeCategoryAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        await EnsureLoadedAsync(cancellationToken);
        return ResolveCategory(value) ?? value.Trim();
    }

    public async Task<IReadOnlyList<NurseCatalogOption>> GetActiveSpecialtiesAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _specialties!
            .OrderBy(row => row.DisplayOrder)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(row => new NurseCatalogOption(row.Code, row.DisplayName))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<NurseCatalogOption>> GetActiveCategoriesAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _categories!
            .OrderBy(row => row.DisplayOrder)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(row => new NurseCatalogOption(row.Code, row.DisplayName))
            .ToList()
            .AsReadOnly();
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_specialties is not null && _categories is not null)
        {
            return;
        }

        _specialties = await _dbContext.NurseSpecialtyCatalogs
            .AsNoTracking()
            .Where(row => row.IsActive)
            .ToListAsync(cancellationToken);

        _categories = await _dbContext.NurseCategoryCatalogs
            .AsNoTracking()
            .Where(row => row.IsActive)
            .ToListAsync(cancellationToken);
    }

    private string? ResolveSpecialty(string value)
    {
        var trimmed = value.Trim();
        foreach (var row in _specialties!)
        {
            if (string.Equals(row.Code, trimmed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return row.Code;
            }

            if (string.IsNullOrWhiteSpace(row.AlternativeCodes))
            {
                continue;
            }

            foreach (var alias in row.AlternativeCodes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return row.Code;
                }
            }
        }

        return null;
    }

    private string? ResolveCategory(string value)
    {
        var trimmed = value.Trim();
        foreach (var row in _categories!)
        {
            if (string.Equals(row.Code, trimmed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return row.Code;
            }

            if (string.IsNullOrWhiteSpace(row.AlternativeCodes))
            {
                continue;
            }

            foreach (var alias in row.AlternativeCodes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return row.Code;
                }
            }
        }

        return null;
    }
}

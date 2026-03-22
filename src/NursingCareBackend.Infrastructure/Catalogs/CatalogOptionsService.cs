using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Catalogs;

public sealed class CatalogOptionsService : ICatalogOptionsService
{
    private readonly NursingCareDbContext _db;

    public CatalogOptionsService(NursingCareDbContext db)
    {
        _db = db;
    }

    public async Task<CatalogOptionsResponse> GetCareRequestOptionsAsync(CancellationToken cancellationToken)
    {
        var categories = await _db.CareRequestCategoryCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CareRequestCategoryOptionItem(x.Code, x.DisplayName, x.CategoryFactor))
            .ToListAsync(cancellationToken);

        var types = await _db.CareRequestTypeCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CareRequestTypeOptionItem(
                x.Code,
                x.DisplayName,
                x.CareRequestCategoryCode,
                x.UnitTypeCode,
                x.BasePrice))
            .ToListAsync(cancellationToken);

        var unitTypes = await _db.UnitTypeCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CatalogOptionItem(x.Code, x.DisplayName))
            .ToListAsync(cancellationToken);

        var distance = await _db.DistanceFactorCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new DistanceFactorOptionItem(x.Code, x.DisplayName, x.Multiplier))
            .ToListAsync(cancellationToken);

        var complexity = await _db.ComplexityLevelCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new ComplexityLevelOptionItem(x.Code, x.DisplayName, x.Multiplier))
            .ToListAsync(cancellationToken);

        var volume = await _db.VolumeDiscountRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new VolumeDiscountOptionItem(x.MinimumCount, x.DiscountPercent))
            .ToListAsync(cancellationToken);

        return new CatalogOptionsResponse(
            categories,
            types,
            unitTypes,
            distance,
            complexity,
            volume);
    }

    public async Task<NurseProfileOptionsResponse> GetNurseProfileOptionsAsync(CancellationToken cancellationToken)
    {
        var specialties = await _db.NurseSpecialtyCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CatalogOptionItem(x.Code, x.DisplayName))
            .ToListAsync(cancellationToken);

        var categories = await _db.NurseCategoryCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CatalogOptionItem(x.Code, x.DisplayName))
            .ToListAsync(cancellationToken);

        return new NurseProfileOptionsResponse(specialties, categories);
    }
}

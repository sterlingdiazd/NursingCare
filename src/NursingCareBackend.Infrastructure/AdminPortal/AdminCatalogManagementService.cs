using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Catalog;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Domain.Catalogs;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminCatalogManagementService : IAdminCatalogManagementService
{
    private readonly NursingCareDbContext _db;
    private readonly IPricingPreviewService _pricingPreview;
    private readonly IAdminNotificationPublisher _notifications;

    public AdminCatalogManagementService(
        NursingCareDbContext db,
        IPricingPreviewService pricingPreview,
        IAdminNotificationPublisher notifications)
    {
        _db = db;
        _pricingPreview = pricingPreview;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<CareRequestCategoryListItemDto>> ListCareRequestCategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.CareRequestCategoryCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => new CareRequestCategoryListItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.CategoryFactor,
                x.IsActive,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CareRequestTypeListItemDto>> ListCareRequestTypesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.CareRequestTypeCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => new CareRequestTypeListItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.CareRequestCategoryCode,
                x.UnitTypeCode,
                x.BasePrice,
                x.IsActive,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnitTypeListItemDto>> ListUnitTypesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.UnitTypeCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => new UnitTypeListItemDto(x.Id, x.Code, x.DisplayName, x.IsActive, x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DistanceFactorListItemDto>> ListDistanceFactorsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.DistanceFactorCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => new DistanceFactorListItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.Multiplier,
                x.IsActive,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplexityLevelListItemDto>> ListComplexityLevelsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.ComplexityLevelCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => new ComplexityLevelListItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.Multiplier,
                x.IsActive,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VolumeDiscountRuleListItemDto>> ListVolumeDiscountRulesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.VolumeDiscountRules.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.MinimumCount)
            .Select(x => new VolumeDiscountRuleListItemDto(
                x.Id,
                x.MinimumCount,
                x.DiscountPercent,
                x.IsActive,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NurseSpecialtyListItemDto>> ListNurseSpecialtiesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.NurseSpecialtyCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => new NurseSpecialtyListItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.AlternativeCodes,
                x.IsActive,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NurseCategoryListItemDto>> ListNurseCategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.NurseCategoryCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => new NurseCategoryListItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.AlternativeCodes,
                x.IsActive,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateCareRequestCategoryAsync(
        Guid id,
        string displayName,
        decimal categoryFactor,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.CareRequestCategoryCatalogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Catalogo de categoria no encontrado.");
        }

        row.DisplayName = displayName.Trim();
        row.CategoryFactor = categoryFactor;
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("categoria de solicitud", row.Code, cancellationToken);
    }

    public async Task UpdateCareRequestTypeAsync(
        Guid id,
        string displayName,
        string careRequestCategoryCode,
        string unitTypeCode,
        decimal basePrice,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.CareRequestTypeCatalogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Catalogo de tipo de solicitud no encontrado.");
        }

        await EnsureCategoryExistsAsync(careRequestCategoryCode, cancellationToken);
        await EnsureUnitTypeExistsAsync(unitTypeCode, cancellationToken);

        row.DisplayName = displayName.Trim();
        row.CareRequestCategoryCode = careRequestCategoryCode.Trim();
        row.UnitTypeCode = unitTypeCode.Trim();
        row.BasePrice = basePrice;
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("tipo de solicitud", row.Code, cancellationToken);
    }

    public async Task UpdateUnitTypeAsync(
        Guid id,
        string displayName,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.UnitTypeCatalogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Catalogo de unidad no encontrado.");
        }

        row.DisplayName = displayName.Trim();
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("tipo de unidad", row.Code, cancellationToken);
    }

    public async Task UpdateDistanceFactorAsync(
        Guid id,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.DistanceFactorCatalogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Catalogo de distancia no encontrado.");
        }

        row.DisplayName = displayName.Trim();
        row.Multiplier = multiplier;
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("factor de distancia", row.Code, cancellationToken);
    }

    public async Task UpdateComplexityLevelAsync(
        Guid id,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.ComplexityLevelCatalogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Catalogo de complejidad no encontrado.");
        }

        row.DisplayName = displayName.Trim();
        row.Multiplier = multiplier;
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("nivel de complejidad", row.Code, cancellationToken);
    }

    public async Task UpdateVolumeDiscountRuleAsync(
        Guid id,
        int minimumCount,
        int discountPercent,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.VolumeDiscountRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Regla de descuento no encontrada.");
        }

        row.MinimumCount = minimumCount;
        row.DiscountPercent = discountPercent;
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("regla de descuento por volumen", row.MinimumCount.ToString(), cancellationToken);
    }

    public async Task UpdateNurseSpecialtyAsync(
        Guid id,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.NurseSpecialtyCatalogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Catalogo de especialidad no encontrado.");
        }

        row.DisplayName = displayName.Trim();
        row.AlternativeCodes = string.IsNullOrWhiteSpace(alternativeCodes) ? null : alternativeCodes.Trim();
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("especialidad de enfermeria", row.Code, cancellationToken);
    }

    public async Task UpdateNurseCategoryAsync(
        Guid id,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = await _db.NurseCategoryCatalogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Catalogo de categoria de enfermeria no encontrado.");
        }

        row.DisplayName = displayName.Trim();
        row.AlternativeCodes = string.IsNullOrWhiteSpace(alternativeCodes) ? null : alternativeCodes.Trim();
        row.IsActive = isActive;
        row.DisplayOrder = displayOrder;
        await SaveAndPublishCatalogChangeAsync("categoria de enfermeria", row.Code, cancellationToken);
    }

    public async Task<Guid> CreateCareRequestCategoryAsync(
        string code,
        string displayName,
        decimal categoryFactor,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        if (await _db.CareRequestCategoryCatalogs.AnyAsync(x => x.Code == normalized, cancellationToken))
        {
            throw new InvalidOperationException("El codigo de categoria ya existe.");
        }

        var row = new CareRequestCategoryCatalog
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            CategoryFactor = categoryFactor,
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.CareRequestCategoryCatalogs.Add(row);
        await SaveAndPublishCatalogChangeAsync("categoria de solicitud", row.Code, cancellationToken);
        return row.Id;
    }

    public async Task<Guid> CreateCareRequestTypeAsync(
        string code,
        string displayName,
        string careRequestCategoryCode,
        string unitTypeCode,
        decimal basePrice,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        if (await _db.CareRequestTypeCatalogs.AnyAsync(x => x.Code == normalized, cancellationToken))
        {
            throw new InvalidOperationException("El codigo de tipo de solicitud ya existe.");
        }

        await EnsureCategoryExistsAsync(careRequestCategoryCode, cancellationToken);
        await EnsureUnitTypeExistsAsync(unitTypeCode, cancellationToken);

        var row = new CareRequestTypeCatalog
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            CareRequestCategoryCode = careRequestCategoryCode.Trim(),
            UnitTypeCode = unitTypeCode.Trim(),
            BasePrice = basePrice,
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.CareRequestTypeCatalogs.Add(row);
        await SaveAndPublishCatalogChangeAsync("tipo de solicitud", row.Code, cancellationToken);
        return row.Id;
    }

    public async Task<Guid> CreateUnitTypeAsync(
        string code,
        string displayName,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        if (await _db.UnitTypeCatalogs.AnyAsync(x => x.Code == normalized, cancellationToken))
        {
            throw new InvalidOperationException("El codigo de unidad ya existe.");
        }

        var row = new UnitTypeCatalog
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.UnitTypeCatalogs.Add(row);
        await SaveAndPublishCatalogChangeAsync("tipo de unidad", row.Code, cancellationToken);
        return row.Id;
    }

    public async Task<Guid> CreateDistanceFactorAsync(
        string code,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        if (await _db.DistanceFactorCatalogs.AnyAsync(x => x.Code == normalized, cancellationToken))
        {
            throw new InvalidOperationException("El codigo de factor de distancia ya existe.");
        }

        var row = new DistanceFactorCatalog
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            Multiplier = multiplier,
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.DistanceFactorCatalogs.Add(row);
        await SaveAndPublishCatalogChangeAsync("factor de distancia", row.Code, cancellationToken);
        return row.Id;
    }

    public async Task<Guid> CreateComplexityLevelAsync(
        string code,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        if (await _db.ComplexityLevelCatalogs.AnyAsync(x => x.Code == normalized, cancellationToken))
        {
            throw new InvalidOperationException("El codigo de complejidad ya existe.");
        }

        var row = new ComplexityLevelCatalog
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            Multiplier = multiplier,
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.ComplexityLevelCatalogs.Add(row);
        await SaveAndPublishCatalogChangeAsync("nivel de complejidad", row.Code, cancellationToken);
        return row.Id;
    }

    public async Task<Guid> CreateVolumeDiscountRuleAsync(
        int minimumCount,
        int discountPercent,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var row = new VolumeDiscountRule
        {
            Id = Guid.NewGuid(),
            MinimumCount = minimumCount,
            DiscountPercent = discountPercent,
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.VolumeDiscountRules.Add(row);
        await SaveAndPublishCatalogChangeAsync("regla de descuento por volumen", row.MinimumCount.ToString(), cancellationToken);
        return row.Id;
    }

    public async Task<Guid> CreateNurseSpecialtyAsync(
        string code,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        if (await _db.NurseSpecialtyCatalogs.AnyAsync(x => x.Code == normalized, cancellationToken))
        {
            throw new InvalidOperationException("El codigo de especialidad ya existe.");
        }

        var row = new NurseSpecialtyCatalog
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            AlternativeCodes = string.IsNullOrWhiteSpace(alternativeCodes) ? null : alternativeCodes.Trim(),
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.NurseSpecialtyCatalogs.Add(row);
        await SaveAndPublishCatalogChangeAsync("especialidad de enfermeria", row.Code, cancellationToken);
        return row.Id;
    }

    public async Task<Guid> CreateNurseCategoryAsync(
        string code,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        if (await _db.NurseCategoryCatalogs.AnyAsync(x => x.Code == normalized, cancellationToken))
        {
            throw new InvalidOperationException("El codigo de categoria ya existe.");
        }

        var row = new NurseCategoryCatalog
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            AlternativeCodes = string.IsNullOrWhiteSpace(alternativeCodes) ? null : alternativeCodes.Trim(),
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

        _db.NurseCategoryCatalogs.Add(row);
        await SaveAndPublishCatalogChangeAsync("categoria de enfermeria", row.Code, cancellationToken);
        return row.Id;
    }

    public Task<PricingPreviewResponse> PreviewPricingAsync(
        PricingPreviewRequest request,
        CancellationToken cancellationToken)
        => _pricingPreview.PreviewAsync(request, cancellationToken);

    private async Task EnsureCategoryExistsAsync(string code, CancellationToken cancellationToken)
    {
        var exists = await _db.CareRequestCategoryCatalogs
            .AsNoTracking()
            .AnyAsync(x => x.Code == code.Trim(), cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("La categoria de solicitud referenciada no existe.");
        }
    }

    private async Task EnsureUnitTypeExistsAsync(string code, CancellationToken cancellationToken)
    {
        var exists = await _db.UnitTypeCatalogs
            .AsNoTracking()
            .AnyAsync(x => x.Code == code.Trim(), cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("El tipo de unidad referenciado no existe.");
        }
    }

    private async Task SaveAndPublishCatalogChangeAsync(
        string catalogName,
        string recordCode,
        CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);

        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "catalog_or_pricing_published",
                Severity: "Medium",
                Title: "Catalogo o precio publicado",
                Body: $"Se publico un cambio en {catalogName} ({recordCode}).",
                EntityType: "Catalog",
                EntityId: recordCode,
                DeepLinkPath: "/admin/catalog",
                Source: "Administracion",
                RequiresAction: false),
            cancellationToken);
    }

}

using NursingCareBackend.Application.Catalogs;

namespace NursingCareBackend.Application.AdminPortal.Catalog;

public interface IAdminCatalogManagementService
{
    Task<IReadOnlyList<CareRequestCategoryListItemDto>> ListCareRequestCategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CareRequestTypeListItemDto>> ListCareRequestTypesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UnitTypeListItemDto>> ListUnitTypesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyList<DistanceFactorListItemDto>> ListDistanceFactorsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ComplexityLevelListItemDto>> ListComplexityLevelsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VolumeDiscountRuleListItemDto>> ListVolumeDiscountRulesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NurseSpecialtyListItemDto>> ListNurseSpecialtiesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NurseCategoryListItemDto>> ListNurseCategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task UpdateCareRequestCategoryAsync(
        Guid id,
        string displayName,
        decimal categoryFactor,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task UpdateCareRequestTypeAsync(
        Guid id,
        string displayName,
        string careRequestCategoryCode,
        string unitTypeCode,
        decimal basePrice,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task UpdateUnitTypeAsync(
        Guid id,
        string displayName,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task UpdateDistanceFactorAsync(
        Guid id,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task UpdateComplexityLevelAsync(
        Guid id,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task UpdateVolumeDiscountRuleAsync(
        Guid id,
        int minimumCount,
        int discountPercent,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task UpdateNurseSpecialtyAsync(
        Guid id,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task UpdateNurseCategoryAsync(
        Guid id,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateCareRequestCategoryAsync(
        string code,
        string displayName,
        decimal categoryFactor,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateCareRequestTypeAsync(
        string code,
        string displayName,
        string careRequestCategoryCode,
        string unitTypeCode,
        decimal basePrice,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateUnitTypeAsync(
        string code,
        string displayName,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateDistanceFactorAsync(
        string code,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateComplexityLevelAsync(
        string code,
        string displayName,
        decimal multiplier,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateVolumeDiscountRuleAsync(
        int minimumCount,
        int discountPercent,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateNurseSpecialtyAsync(
        string code,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<Guid> CreateNurseCategoryAsync(
        string code,
        string displayName,
        string? alternativeCodes,
        bool isActive,
        int displayOrder,
        CancellationToken cancellationToken);

    Task<PricingPreviewResponse> PreviewPricingAsync(
        PricingPreviewRequest request,
        CancellationToken cancellationToken);
}

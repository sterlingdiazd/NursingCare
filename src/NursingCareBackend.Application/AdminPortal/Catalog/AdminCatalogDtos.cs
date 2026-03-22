namespace NursingCareBackend.Application.AdminPortal.Catalog;

public sealed record CareRequestCategoryListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    decimal CategoryFactor,
    bool IsActive,
    int DisplayOrder);

public sealed record CareRequestTypeListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    string CareRequestCategoryCode,
    string UnitTypeCode,
    decimal BasePrice,
    bool IsActive,
    int DisplayOrder);

public sealed record UnitTypeListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    bool IsActive,
    int DisplayOrder);

public sealed record DistanceFactorListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    decimal Multiplier,
    bool IsActive,
    int DisplayOrder);

public sealed record ComplexityLevelListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    decimal Multiplier,
    bool IsActive,
    int DisplayOrder);

public sealed record VolumeDiscountRuleListItemDto(
    Guid Id,
    int MinimumCount,
    int DiscountPercent,
    bool IsActive,
    int DisplayOrder);

public sealed record NurseSpecialtyListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    string? AlternativeCodes,
    bool IsActive,
    int DisplayOrder);

public sealed record NurseCategoryListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    string? AlternativeCodes,
    bool IsActive,
    int DisplayOrder);

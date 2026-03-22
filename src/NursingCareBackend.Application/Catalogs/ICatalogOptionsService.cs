namespace NursingCareBackend.Application.Catalogs;

public interface ICatalogOptionsService
{
    Task<CatalogOptionsResponse> GetCareRequestOptionsAsync(CancellationToken cancellationToken);

    Task<NurseProfileOptionsResponse> GetNurseProfileOptionsAsync(CancellationToken cancellationToken);
}

public sealed record CatalogOptionsResponse(
    IReadOnlyList<CareRequestCategoryOptionItem> CareRequestCategories,
    IReadOnlyList<CareRequestTypeOptionItem> CareRequestTypes,
    IReadOnlyList<CatalogOptionItem> UnitTypes,
    IReadOnlyList<DistanceFactorOptionItem> DistanceFactors,
    IReadOnlyList<ComplexityLevelOptionItem> ComplexityLevels,
    IReadOnlyList<VolumeDiscountOptionItem> VolumeDiscountRules);

public sealed record CareRequestCategoryOptionItem(string Code, string DisplayName, decimal CategoryFactor);

public sealed record CareRequestTypeOptionItem(
    string Code,
    string DisplayName,
    string CareRequestCategoryCode,
    string UnitTypeCode,
    decimal BasePrice);

public sealed record DistanceFactorOptionItem(string Code, string DisplayName, decimal Multiplier);

public sealed record ComplexityLevelOptionItem(string Code, string DisplayName, decimal Multiplier);

public sealed record NurseProfileOptionsResponse(
    IReadOnlyList<CatalogOptionItem> Specialties,
    IReadOnlyList<CatalogOptionItem> Categories);

public sealed record CatalogOptionItem(string Code, string DisplayName);

public sealed record VolumeDiscountOptionItem(int MinimumCount, int DiscountPercent);

namespace NursingCareBackend.Domain.Catalogs;

public sealed class CareRequestTypeCatalog
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string CareRequestCategoryCode { get; set; } = default!;

    public string UnitTypeCode { get; set; } = default!;

    public decimal BasePrice { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}

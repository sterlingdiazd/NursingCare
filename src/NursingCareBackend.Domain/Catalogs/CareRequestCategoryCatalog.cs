namespace NursingCareBackend.Domain.Catalogs;

public sealed class CareRequestCategoryCatalog
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public decimal CategoryFactor { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}

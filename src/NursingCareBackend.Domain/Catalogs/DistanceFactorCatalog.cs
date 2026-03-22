namespace NursingCareBackend.Domain.Catalogs;

public sealed class DistanceFactorCatalog
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public decimal Multiplier { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}

namespace NursingCareBackend.Domain.Catalogs;

public sealed class VolumeDiscountRule
{
    public Guid Id { get; set; }

    public int MinimumCount { get; set; }

    public int DiscountPercent { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}

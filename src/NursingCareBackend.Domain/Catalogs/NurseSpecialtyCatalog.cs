namespace NursingCareBackend.Domain.Catalogs;

public sealed class NurseSpecialtyCatalog
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? AlternativeCodes { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}

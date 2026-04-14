namespace NursingCareBackend.Domain.Payroll;

public sealed class CompensationRule
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public CompensationEmploymentType EmploymentType { get; private set; }
    public string? CareRequestCategoryCode { get; private set; }
    public string? UnitTypeCode { get; private set; }
    public string? NurseCategoryCode { get; private set; }
    public decimal BaseCompensationPercent { get; private set; }
    public decimal FixedAmountPerUnit { get; private set; }
    public decimal TransportIncentivePercent { get; private set; }
    public decimal ComplexityBonusPercent { get; private set; }
    public decimal MedicalSuppliesPercent { get; private set; }
    public decimal PartialServicePercent { get; private set; }
    public decimal ExpressServicePercent { get; private set; }
    public decimal SuspendedServicePercent { get; private set; }
    public bool IsActive { get; private set; }
    public int Priority { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CompensationRule() { }

    public static CompensationRule Create(
        string name,
        CompensationEmploymentType employmentType,
        string? careRequestCategoryCode,
        string? unitTypeCode,
        string? nurseCategoryCode,
        decimal baseCompensationPercent,
        decimal fixedAmountPerUnit,
        decimal transportIncentivePercent,
        decimal complexityBonusPercent,
        decimal medicalSuppliesPercent,
        decimal partialServicePercent,
        decimal expressServicePercent,
        decimal suspendedServicePercent,
        bool isActive,
        int priority,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Rule name is required.", nameof(name));
        }

        return new CompensationRule
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            EmploymentType = employmentType,
            CareRequestCategoryCode = Normalize(careRequestCategoryCode),
            UnitTypeCode = Normalize(unitTypeCode),
            NurseCategoryCode = Normalize(nurseCategoryCode),
            BaseCompensationPercent = Clamp(baseCompensationPercent),
            FixedAmountPerUnit = Clamp(fixedAmountPerUnit),
            TransportIncentivePercent = Clamp(transportIncentivePercent),
            ComplexityBonusPercent = Clamp(complexityBonusPercent),
            MedicalSuppliesPercent = Clamp(medicalSuppliesPercent),
            PartialServicePercent = Clamp(partialServicePercent),
            ExpressServicePercent = Clamp(expressServicePercent),
            SuspendedServicePercent = Clamp(suspendedServicePercent),
            IsActive = isActive,
            Priority = priority,
            CreatedAtUtc = createdAtUtc,
        };
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal Clamp(decimal value)
        => decimal.Round(Math.Max(0m, value), 4, MidpointRounding.AwayFromZero);
}

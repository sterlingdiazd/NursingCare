namespace NursingCareBackend.Domain.Payroll;

public sealed class ServiceExecution
{
    public Guid Id { get; private set; }
    public Guid CareRequestId { get; private set; }
    public Guid NurseUserId { get; private set; }
    public Guid? ShiftRecordId { get; private set; }
    public Guid? CompensationRuleId { get; private set; }
    public CompensationEmploymentType EmploymentType { get; private set; }
    public ServiceExecutionVariant Variant { get; private set; }
    public DateTime ExecutedAtUtc { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public string CareRequestType { get; private set; } = default!;
    public string UnitType { get; private set; } = default!;
    public int Unit { get; private set; }
    public string? PricingCategoryCode { get; private set; }
    public string? DistanceFactorCode { get; private set; }
    public string? ComplexityLevelCode { get; private set; }
    public decimal BasePrice { get; private set; }
    public decimal CareRequestTotal { get; private set; }
    public decimal ClientBasePrice { get; private set; }
    public decimal CategoryFactorSnapshot { get; private set; }
    public decimal DistanceMultiplierSnapshot { get; private set; }
    public decimal ComplexityMultiplierSnapshot { get; private set; }
    public int VolumeDiscountPercentSnapshot { get; private set; }
    public decimal SubtotalBeforeSupplies { get; private set; }
    public decimal MedicalSuppliesCost { get; private set; }
    public decimal RuleBaseCompensationPercent { get; private set; }
    public decimal RuleFixedAmountPerUnit { get; private set; }
    public decimal RuleTransportIncentivePercent { get; private set; }
    public decimal RuleComplexityBonusPercent { get; private set; }
    public decimal RuleMedicalSuppliesPercent { get; private set; }
    public decimal RuleVariantPercent { get; private set; }
    public decimal BaseCompensation { get; private set; }
    public decimal TransportIncentive { get; private set; }
    public decimal ComplexityBonus { get; private set; }
    public decimal MedicalSuppliesCompensation { get; private set; }
    public decimal AdjustmentsTotal { get; private set; }
    public decimal DeductionsTotal { get; private set; }
    public decimal GrossCompensation { get; private set; }
    public decimal NetCompensation { get; private set; }
    public decimal? ManualOverrideAmount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ServiceExecution() { }

    public static ServiceExecution Create(
        Guid careRequestId,
        Guid nurseUserId,
        Guid? shiftRecordId,
        Guid? compensationRuleId,
        CompensationEmploymentType employmentType,
        ServiceExecutionVariant variant,
        DateTime executedAtUtc,
        string careRequestType,
        string unitType,
        int unit,
        string? pricingCategoryCode,
        string? distanceFactorCode,
        string? complexityLevelCode,
        decimal basePrice,
        decimal careRequestTotal,
        decimal clientBasePrice,
        decimal categoryFactorSnapshot,
        decimal distanceMultiplierSnapshot,
        decimal complexityMultiplierSnapshot,
        int volumeDiscountPercentSnapshot,
        decimal subtotalBeforeSupplies,
        decimal medicalSuppliesCost,
        decimal ruleBaseCompensationPercent,
        decimal ruleFixedAmountPerUnit,
        decimal ruleTransportIncentivePercent,
        decimal ruleComplexityBonusPercent,
        decimal ruleMedicalSuppliesPercent,
        decimal ruleVariantPercent,
        decimal baseCompensation,
        decimal transportIncentive,
        decimal complexityBonus,
        decimal medicalSuppliesCompensation,
        decimal adjustmentsTotal,
        decimal deductionsTotal,
        decimal? manualOverrideAmount,
        string? notes,
        DateTime createdAtUtc)
    {
        if (careRequestId == Guid.Empty)
        {
            throw new ArgumentException("Care request is required.", nameof(careRequestId));
        }

        if (nurseUserId == Guid.Empty)
        {
            throw new ArgumentException("Nurse user is required.", nameof(nurseUserId));
        }

        if (string.IsNullOrWhiteSpace(careRequestType))
        {
            throw new ArgumentException("Care request type is required.", nameof(careRequestType));
        }

        if (string.IsNullOrWhiteSpace(unitType))
        {
            throw new ArgumentException("Unit type is required.", nameof(unitType));
        }

        if (unit <= 0)
        {
            throw new ArgumentException("Unit must be greater than zero.", nameof(unit));
        }

        var grossCompensation = baseCompensation + transportIncentive + complexityBonus + medicalSuppliesCompensation + adjustmentsTotal;
        var resolvedNet = manualOverrideAmount ?? (grossCompensation - deductionsTotal);

        return new ServiceExecution
        {
            Id = Guid.NewGuid(),
            CareRequestId = careRequestId,
            NurseUserId = nurseUserId,
            ShiftRecordId = shiftRecordId,
            CompensationRuleId = compensationRuleId,
            EmploymentType = employmentType,
            Variant = variant,
            ExecutedAtUtc = executedAtUtc,
            ServiceDate = DateOnly.FromDateTime(executedAtUtc),
            CareRequestType = careRequestType.Trim(),
            UnitType = unitType.Trim(),
            Unit = unit,
            PricingCategoryCode = Normalize(pricingCategoryCode),
            DistanceFactorCode = Normalize(distanceFactorCode),
            ComplexityLevelCode = Normalize(complexityLevelCode),
            BasePrice = Round(basePrice),
            CareRequestTotal = Round(careRequestTotal),
            ClientBasePrice = Round(clientBasePrice),
            CategoryFactorSnapshot = Round4(categoryFactorSnapshot),
            DistanceMultiplierSnapshot = Round4(distanceMultiplierSnapshot),
            ComplexityMultiplierSnapshot = Round4(complexityMultiplierSnapshot),
            VolumeDiscountPercentSnapshot = volumeDiscountPercentSnapshot,
            SubtotalBeforeSupplies = Round(subtotalBeforeSupplies),
            MedicalSuppliesCost = Round(medicalSuppliesCost),
            RuleBaseCompensationPercent = Round4(ruleBaseCompensationPercent),
            RuleFixedAmountPerUnit = Round(ruleFixedAmountPerUnit),
            RuleTransportIncentivePercent = Round4(ruleTransportIncentivePercent),
            RuleComplexityBonusPercent = Round4(ruleComplexityBonusPercent),
            RuleMedicalSuppliesPercent = Round4(ruleMedicalSuppliesPercent),
            RuleVariantPercent = Round4(ruleVariantPercent),
            BaseCompensation = Round(baseCompensation),
            TransportIncentive = Round(transportIncentive),
            ComplexityBonus = Round(complexityBonus),
            MedicalSuppliesCompensation = Round(medicalSuppliesCompensation),
            AdjustmentsTotal = Round(adjustmentsTotal),
            DeductionsTotal = Round(deductionsTotal),
            GrossCompensation = Round(grossCompensation),
            NetCompensation = Round(resolvedNet),
            ManualOverrideAmount = manualOverrideAmount.HasValue ? Round(manualOverrideAmount.Value) : null,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };
    }

    public void Refresh(
        Guid? compensationRuleId,
        CompensationEmploymentType employmentType,
        ServiceExecutionVariant variant,
        DateTime executedAtUtc,
        decimal ruleBaseCompensationPercent,
        decimal ruleFixedAmountPerUnit,
        decimal ruleTransportIncentivePercent,
        decimal ruleComplexityBonusPercent,
        decimal ruleMedicalSuppliesPercent,
        decimal ruleVariantPercent,
        decimal baseCompensation,
        decimal transportIncentive,
        decimal complexityBonus,
        decimal medicalSuppliesCompensation,
        decimal adjustmentsTotal,
        decimal deductionsTotal,
        decimal? manualOverrideAmount,
        string? notes,
        DateTime updatedAtUtc)
    {
        CompensationRuleId = compensationRuleId;
        EmploymentType = employmentType;
        Variant = variant;
        ExecutedAtUtc = executedAtUtc;
        ServiceDate = DateOnly.FromDateTime(executedAtUtc);
        RuleBaseCompensationPercent = Round4(ruleBaseCompensationPercent);
        RuleFixedAmountPerUnit = Round(ruleFixedAmountPerUnit);
        RuleTransportIncentivePercent = Round4(ruleTransportIncentivePercent);
        RuleComplexityBonusPercent = Round4(ruleComplexityBonusPercent);
        RuleMedicalSuppliesPercent = Round4(ruleMedicalSuppliesPercent);
        RuleVariantPercent = Round4(ruleVariantPercent);
        BaseCompensation = Round(baseCompensation);
        TransportIncentive = Round(transportIncentive);
        ComplexityBonus = Round(complexityBonus);
        MedicalSuppliesCompensation = Round(medicalSuppliesCompensation);
        AdjustmentsTotal = Round(adjustmentsTotal);
        DeductionsTotal = Round(deductionsTotal);
        GrossCompensation = Round(BaseCompensation + TransportIncentive + ComplexityBonus + MedicalSuppliesCompensation + AdjustmentsTotal);
        ManualOverrideAmount = manualOverrideAmount.HasValue ? Round(manualOverrideAmount.Value) : null;
        NetCompensation = Round(ManualOverrideAmount ?? (GrossCompensation - DeductionsTotal));
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round4(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

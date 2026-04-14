namespace NursingCareBackend.Domain.Payroll;

public sealed class PayrollLine
{
    public Guid Id { get; private set; }
    public Guid PayrollPeriodId { get; private set; }
    public Guid NurseUserId { get; private set; }
    public Guid? ServiceExecutionId { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal BaseCompensation { get; private set; }
    public decimal TransportIncentive { get; private set; }
    public decimal ComplexityBonus { get; private set; }
    public decimal MedicalSuppliesCompensation { get; private set; }
    public decimal AdjustmentsTotal { get; private set; }
    public decimal DeductionsTotal { get; private set; }
    public decimal NetCompensation { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PayrollLine() { }

    public static PayrollLine Create(
        Guid payrollPeriodId,
        Guid nurseUserId,
        Guid? serviceExecutionId,
        string description,
        decimal baseCompensation,
        decimal transportIncentive,
        decimal complexityBonus,
        decimal medicalSuppliesCompensation,
        decimal adjustmentsTotal,
        decimal deductionsTotal,
        DateTime createdAtUtc)
    {
        if (payrollPeriodId == Guid.Empty)
        {
            throw new ArgumentException("Payroll period is required.", nameof(payrollPeriodId));
        }

        if (nurseUserId == Guid.Empty)
        {
            throw new ArgumentException("Nurse user is required.", nameof(nurseUserId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Payroll line description is required.", nameof(description));
        }

        var net = baseCompensation + transportIncentive + complexityBonus + medicalSuppliesCompensation + adjustmentsTotal - deductionsTotal;

        return new PayrollLine
        {
            Id = Guid.NewGuid(),
            PayrollPeriodId = payrollPeriodId,
            NurseUserId = nurseUserId,
            ServiceExecutionId = serviceExecutionId,
            Description = description.Trim(),
            BaseCompensation = Round(baseCompensation),
            TransportIncentive = Round(transportIncentive),
            ComplexityBonus = Round(complexityBonus),
            MedicalSuppliesCompensation = Round(medicalSuppliesCompensation),
            AdjustmentsTotal = Round(adjustmentsTotal),
            DeductionsTotal = Round(deductionsTotal),
            NetCompensation = Round(net),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };
    }

    public void RefreshAmounts(
        decimal baseCompensation,
        decimal transportIncentive,
        decimal complexityBonus,
        decimal medicalSuppliesCompensation,
        decimal adjustmentsTotal,
        decimal deductionsTotal,
        DateTime updatedAtUtc)
    {
        BaseCompensation = Round(baseCompensation);
        TransportIncentive = Round(transportIncentive);
        ComplexityBonus = Round(complexityBonus);
        MedicalSuppliesCompensation = Round(medicalSuppliesCompensation);
        AdjustmentsTotal = Round(adjustmentsTotal);
        DeductionsTotal = Round(deductionsTotal);
        NetCompensation = Round(BaseCompensation + TransportIncentive + ComplexityBonus + MedicalSuppliesCompensation + AdjustmentsTotal - DeductionsTotal);
        UpdatedAtUtc = updatedAtUtc;
    }

    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

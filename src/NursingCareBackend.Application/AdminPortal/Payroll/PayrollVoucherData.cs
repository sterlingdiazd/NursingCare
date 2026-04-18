namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed class PayrollVoucherData
{
    public Guid PeriodId { get; init; }
    public DateOnly PeriodStartDate { get; init; }
    public DateOnly PeriodEndDate { get; init; }
    public DateOnly PaymentDate { get; init; }
    public string PeriodStatus { get; init; } = default!;

    public Guid NurseUserId { get; init; }
    public string NurseDisplayName { get; init; } = default!;
    public string? NurseCedula { get; init; }

    public IReadOnlyList<VoucherLineItem> Lines { get; init; } = [];
    public IReadOnlyList<VoucherDeductionItem> Deductions { get; init; } = [];

    public decimal TotalGross { get; init; }
    public decimal TotalTransport { get; init; }
    public decimal TotalComplexity { get; init; }
    public decimal TotalSupplies { get; init; }
    public decimal TotalAdjustments { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetCompensation { get; init; }
}

public sealed class VoucherLineItem
{
    public string Description { get; init; } = default!;
    public decimal BaseCompensation { get; init; }
    public decimal TransportIncentive { get; init; }
    public decimal ComplexityBonus { get; init; }
    public decimal MedicalSuppliesCompensation { get; init; }
    public decimal AdjustmentsTotal { get; init; }
    public decimal DeductionsTotal { get; init; }
    public decimal NetCompensation { get; init; }
}

public sealed class VoucherDeductionItem
{
    public string Label { get; init; } = default!;
    public string DeductionTypeLabel { get; init; } = default!;
    public decimal Amount { get; init; }
}

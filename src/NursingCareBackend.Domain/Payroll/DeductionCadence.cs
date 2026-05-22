namespace NursingCareBackend.Domain.Payroll;

public enum DeductionCadence
{
    /// <summary>One installment every payroll period (quincenal).</summary>
    PerPeriod = 0,

    /// <summary>One installment per calendar month, applied in the month-closing (16-end) period.</summary>
    Monthly = 1,
}

namespace NursingCareBackend.Domain.Payroll;

public enum ScheduledDeductionStatus
{
    /// <summary>Still generating installments.</summary>
    Active = 0,

    /// <summary>Fully settled (amortizing paid off, or recurring reached its end).</summary>
    Completed = 1,

    /// <summary>Cancelled/voided by an admin; no further installments are generated.</summary>
    Cancelled = 2,
}

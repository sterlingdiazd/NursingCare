namespace NursingCareBackend.Domain.Payroll;

public enum ScheduleModality
{
    /// <summary>Principal paid down in installments with a balance that closes when settled (loans, advances).</summary>
    Amortizing = 0,

    /// <summary>Fixed amount charged each applicable period with no balance (insurance, dues).</summary>
    RecurringFixed = 1,

    /// <summary>A single ad-hoc deduction applied once.</summary>
    OneTime = 2,
}

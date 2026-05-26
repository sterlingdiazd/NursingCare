namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollStaffSummary(
    Guid NurseUserId,
    string NurseDisplayName,
    int LineCount,
    decimal GrossCompensation,
    decimal TransportIncentives,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation,
    // Real payment state for this nurse in this period (Pending/SentToBank/Confirmed/Failed/
    // Reversed), or null if no payment row exists yet. Additive/optional.
    string? PaymentStatus = null,
    string? BankReference = null
);

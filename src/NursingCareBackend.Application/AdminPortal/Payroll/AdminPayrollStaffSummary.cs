namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollStaffSummary(
    Guid NurseUserId,
    string NurseDisplayName,
    int LineCount,
    decimal GrossCompensation,
    decimal TransportIncentives,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation
);

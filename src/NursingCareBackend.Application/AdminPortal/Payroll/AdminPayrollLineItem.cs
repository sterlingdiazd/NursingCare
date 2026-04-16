namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollLineItem(
    Guid Id,
    Guid NurseUserId,
    string NurseDisplayName,
    Guid? ServiceExecutionId,
    string Description,
    decimal BaseCompensation,
    decimal TransportIncentive,
    decimal ComplexityBonus,
    decimal MedicalSuppliesCompensation,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation,
    DateTime CreatedAtUtc
);

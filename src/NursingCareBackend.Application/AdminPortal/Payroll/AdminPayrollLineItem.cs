namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollLineItem(
    Guid Id,
    Guid NurseUserId,
    string NurseDisplayName,
    Guid? ServiceExecutionId,
    Guid? CareRequestId,
    string Description,
    decimal BaseCompensation,
    decimal TransportIncentive,
    decimal ComplexityBonus,
    decimal MedicalSuppliesCompensation,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation,
    decimal ServiceSubtotal, // subtotal cobrado al cliente (antes de insumos); para margen = ServiceSubtotal - NetCompensation
    DateTime CreatedAtUtc
);

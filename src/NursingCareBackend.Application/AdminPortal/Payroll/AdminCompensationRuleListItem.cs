namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminCompensationRuleListItem(
    Guid Id,
    string Name,
    string EmploymentType,  // "Fixed" (monto fijo) | "PerService" (% del precio)
    decimal BaseCompensationPercent,
    decimal FixedAmountPerUnit,
    decimal TransportIncentivePercent,
    decimal ComplexityBonusPercent,
    decimal MedicalSuppliesPercent,
    bool IsActive,
    DateTime CreatedAtUtc
);
namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminCompensationRuleListItem(
    Guid Id,
    string Name,
    string EmploymentType,  // "FullTime" | "PartTime" | "Contractor"
    decimal BaseCompensationPercent,
    decimal TransportIncentivePercent,
    decimal ComplexityBonusPercent,
    decimal MedicalSuppliesPercent,
    bool IsActive,
    DateTime CreatedAtUtc
);
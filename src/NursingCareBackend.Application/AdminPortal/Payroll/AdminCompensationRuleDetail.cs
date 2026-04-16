namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminCompensationRuleDetail(
    Guid Id,
    string Name,
    string EmploymentType,
    decimal BaseCompensationPercent,
    decimal TransportIncentivePercent,
    decimal ComplexityBonusPercent,
    decimal MedicalSuppliesPercent,
    bool IsActive,
    DateTime CreatedAtUtc
);
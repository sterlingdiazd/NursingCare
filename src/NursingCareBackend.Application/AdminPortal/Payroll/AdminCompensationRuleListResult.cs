namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminCompensationRuleListResult(
    IReadOnlyList<AdminCompensationRuleListItem> Items,
    int TotalCount
);
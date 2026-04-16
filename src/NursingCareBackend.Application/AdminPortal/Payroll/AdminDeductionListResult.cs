namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminDeductionListResult(
    IReadOnlyList<AdminDeductionListItem> Items,
    int TotalCount
);
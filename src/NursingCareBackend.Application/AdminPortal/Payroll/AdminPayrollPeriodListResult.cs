namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollPeriodListResult(
    IReadOnlyList<AdminPayrollPeriodListItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

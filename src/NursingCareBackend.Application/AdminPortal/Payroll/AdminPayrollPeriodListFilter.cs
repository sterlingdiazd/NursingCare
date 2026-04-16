namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollPeriodListFilter(
    int PageNumber,
    int PageSize,
    string? Status   // null = all, "Open", "Closed"
);

namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminShiftListResult(
    IReadOnlyList<AdminShiftRecordListItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminCompensationAdjustmentListResult(
    IReadOnlyList<AdminCompensationAdjustmentListItem> Items,
    int TotalCount
);
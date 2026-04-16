namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminCompensationAdjustmentListItem(
    Guid Id,
    Guid ServiceExecutionId,
    string NurseDisplayName,
    string Label,
    decimal Amount,
    DateTime CreatedAtUtc
);
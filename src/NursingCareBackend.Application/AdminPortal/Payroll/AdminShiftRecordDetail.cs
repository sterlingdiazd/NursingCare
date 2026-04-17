namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminShiftRecordDetail(
    Guid Id,
    Guid CareRequestId,
    Guid NurseUserId,
    string NurseDisplayName,
    string ScheduledStartUtc,
    string? ScheduledEndUtc,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<AdminShiftChangeItem> ChangeHistory
);
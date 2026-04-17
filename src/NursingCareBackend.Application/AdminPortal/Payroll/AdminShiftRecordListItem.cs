namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminShiftRecordListItem(
    Guid Id,
    Guid CareRequestId,
    Guid NurseUserId,
    string NurseDisplayName,
    string ScheduledStartUtc,
    string? ScheduledEndUtc,
    string Status,
    DateTime CreatedAtUtc
);
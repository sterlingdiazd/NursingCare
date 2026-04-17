namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminShiftChangeItem(
    Guid Id,
    Guid? PreviousNurseUserId,
    string? PreviousNurseDisplayName,
    Guid? NewNurseUserId,
    string? NewNurseDisplayName,
    string Reason,
    DateTime EffectiveAtUtc,
    DateTime CreatedAtUtc
);
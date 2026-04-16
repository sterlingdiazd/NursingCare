namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminDeductionListItem(
    Guid Id,
    Guid NurseUserId,
    string NurseDisplayName,
    Guid? PayrollPeriodId,
    string Label,
    decimal Amount,
    string DeductionType,
    DateTime CreatedAtUtc
);
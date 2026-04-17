namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminShiftListFilter(
    int PageNumber,
    int PageSize,
    Guid? NurseUserId,
    Guid? CareRequestId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Status
);
namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollPeriodListItem(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly CutoffDate,
    DateOnly PaymentDate,
    string Status,           // "Open" | "Closed"
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    int LineCount
);

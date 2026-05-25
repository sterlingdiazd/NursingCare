namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record AdminPayrollPeriodDetail(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly CutoffDate,
    DateOnly PaymentDate,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    IReadOnlyList<AdminPayrollLineItem> Lines,
    IReadOnlyList<AdminPayrollStaffSummary> StaffSummary,
    // True only while the period is Open and has no calculated lines and no
    // deductions/installments — i.e. it can still be edited or deleted.
    bool CanModify,
    // Reopen audit (most recent reopen + running count), surfaced so the detail header
    // can show that a closed period was reopened for correction.
    DateTime? ReopenedAtUtc,
    string? ReopenReason,
    int ReopenCount
);

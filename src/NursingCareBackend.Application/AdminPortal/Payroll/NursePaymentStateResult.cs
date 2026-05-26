namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>Outcome of a nurse-payment state change (mark-failed / reverse). camelCase JSON.</summary>
public sealed record NursePaymentStateResult(
    Guid PeriodId,
    Guid NurseUserId,
    string PaymentStatus,
    string? Reason);

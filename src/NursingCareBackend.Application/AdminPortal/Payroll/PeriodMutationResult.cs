namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// Outcome of an attempt to edit or delete a payroll period. A period may only be
/// mutated while it is Open and unused (no payroll lines and no deductions/installments
/// reference it); otherwise the attempt is rejected to protect settled records.
/// </summary>
public enum PeriodMutationResult
{
    Success,
    NotFound,
    Closed,
    InUse,
}

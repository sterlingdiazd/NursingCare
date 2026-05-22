namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed class UpdateCompensationAdjustmentRequest
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

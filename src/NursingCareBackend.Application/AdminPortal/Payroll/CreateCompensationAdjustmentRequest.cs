namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed class CreateCompensationAdjustmentRequest
{
    public Guid ServiceExecutionId { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
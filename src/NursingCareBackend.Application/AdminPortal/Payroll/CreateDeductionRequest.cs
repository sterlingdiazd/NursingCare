namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed class CreateDeductionRequest
{
    public Guid NurseUserId { get; set; }
    public Guid? PayrollPeriodId { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string DeductionType { get; set; } = "Fixed";  // "Fixed" | "Percentage"
}
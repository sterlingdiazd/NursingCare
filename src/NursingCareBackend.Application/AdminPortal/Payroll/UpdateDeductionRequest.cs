namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed class UpdateDeductionRequest
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string DeductionType { get; set; } = "Other";  // Loan | Advance | Insurance | Other
}

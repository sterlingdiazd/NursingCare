namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed class UpdateCompensationRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal BaseCompensationPercent { get; set; }
    public decimal TransportIncentivePercent { get; set; }
    public decimal ComplexityBonusPercent { get; set; }
    public decimal MedicalSuppliesPercent { get; set; }
}
namespace NursingCareBackend.Infrastructure.Payroll;

public sealed class CompanyInfoOptions
{
    public const string SectionName = "CompanyInfo";
    public string Name { get; set; } = "NursingCare";
    public string? Rnc { get; set; }
}

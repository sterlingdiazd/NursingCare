namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>Company/accounting data shown on vouchers and reports. Editable by the owner (SystemSettings).</summary>
public sealed record CompanyInfo(string Name, string? Rnc, string? Phone, string? Address);

public interface ICompanyInfoProvider
{
    Task<CompanyInfo> GetAsync(CancellationToken cancellationToken = default);
}

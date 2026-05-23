using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Payroll;

/// <summary>
/// Reads company info from the editable SystemSettings (COMPANY_*) at runtime, falling back to
/// the appsettings <see cref="CompanyInfoOptions"/> defaults. This makes the owner-editable values
/// flow dynamically into every voucher/report PDF without a redeploy.
/// </summary>
public sealed class CompanyInfoProvider : ICompanyInfoProvider
{
    private readonly NursingCareDbContext _db;
    private readonly CompanyInfoOptions _fallback;

    public CompanyInfoProvider(NursingCareDbContext db, IOptions<CompanyInfoOptions> fallback)
    {
        _db = db;
        _fallback = fallback.Value;
    }

    public async Task<CompanyInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var keys = new[] { "COMPANY_NAME", "COMPANY_RNC", "COMPANY_PHONE", "COMPANY_ADDRESS" };
        var values = await _db.SystemSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string? Val(string key) =>
            values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        var name = Val("COMPANY_NAME")
            ?? (string.IsNullOrWhiteSpace(_fallback.Name) ? "Sol y Luna" : _fallback.Name);

        return new CompanyInfo(name, Val("COMPANY_RNC") ?? _fallback.Rnc, Val("COMPANY_PHONE"), Val("COMPANY_ADDRESS"));
    }
}

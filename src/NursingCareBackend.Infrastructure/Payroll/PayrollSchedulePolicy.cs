using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Payroll;

/// <summary>
/// Resolves owner-configurable payroll schedule policy from the editable SystemSettings (PAYROLL_*)
/// at runtime, so the owner can adjust it from Menú → Configuración without a redeploy. Currently
/// surfaces the cutoff offset (PAYROLL_CUTOFF_DAYS_BEFORE_END): how many days before the period end
/// the accounting cutoff falls. Read fresh per call (mirrors CompanyInfoProvider/FiscalSettingsProvider).
/// </summary>
public sealed class PayrollSchedulePolicy : IPayrollSchedulePolicy
{
    public const string CutoffDaysBeforeEndKey = "PAYROLL_CUTOFF_DAYS_BEFORE_END";

    // Default reproduces the historical hardcoded behavior (cutoff = end − 2 days).
    public const int DefaultCutoffDaysBeforeEnd = 2;

    private readonly NursingCareDbContext _db;

    public PayrollSchedulePolicy(NursingCareDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetCutoffDaysBeforeEndAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == CutoffDaysBeforeEndKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw.Trim(), out var days) && days >= 0)
        {
            return days;
        }

        return DefaultCutoffDaysBeforeEnd;
    }

    public async Task<DateOnly> ResolveCutoffDateAsync(DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var daysBefore = await GetCutoffDaysBeforeEndAsync(cancellationToken);
        return endDate.AddDays(-daysBefore);
    }
}

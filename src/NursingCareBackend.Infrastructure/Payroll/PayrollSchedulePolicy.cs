using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Payroll;

/// <summary>
/// Resolves the owner-configurable payroll schedule policy from the editable SystemSettings
/// (PAYROLL_*) at runtime, so the owner can adjust it from Menú → Configuración without a redeploy.
/// The cutoff offset (PAYROLL_CUTOFF_DAYS_BEFORE_END) is the single source of truth for the cutoff;
/// the payment date layers the configurable payment-date policy (mode, fixed payment days) on top
/// of that same cutoff. Missing / blank / non-numeric values fall back to the defaults, so behavior
/// is robust even on a DB whose settings rows were never seeded.
/// </summary>
public sealed class PayrollSchedulePolicy : IPayrollSchedulePolicy
{
    public const string CutoffDaysBeforeEndKey = "PAYROLL_CUTOFF_DAYS_BEFORE_END";

    // Default reproduces the historical hardcoded behavior (cutoff = end − 2 days).
    public const int DefaultCutoffDaysBeforeEnd = 2;

    // Payment-date policy keys — the single backend source of truth for these strings.
    private const string KeyMode = "PAYROLL_PAYMENT_DATE_MODE";
    private const string KeyFirstHalfDay = "PAYROLL_FIRST_HALF_PAYMENT_DAY";
    private const string KeySecondHalfDay = "PAYROLL_SECOND_HALF_PAYMENT_DAY";
    private const string KeyDaysBeforeMonthEnd = "PAYROLL_DAYS_BEFORE_MONTH_END";

    private readonly NursingCareDbContext _dbContext;

    public PayrollSchedulePolicy(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GetCutoffDaysBeforeEndAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _dbContext.SystemSettings.AsNoTracking()
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

    public async Task<(DateOnly Cutoff, DateOnly Payment)> ComputeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        var cutoffDaysBeforeEnd = await GetCutoffDaysBeforeEndAsync(cancellationToken);
        var config = await ReadConfigAsync(cancellationToken);
        return PayrollScheduleCalculator.Compute(start, end, config, cutoffDaysBeforeEnd);
    }

    private async Task<PayrollScheduleConfig> ReadConfigAsync(CancellationToken cancellationToken)
    {
        // One round-trip: pull just the four policy rows (cached for this call's duration).
        var keys = new[] { KeyMode, KeyFirstHalfDay, KeySecondHalfDay, KeyDaysBeforeMonthEnd };
        var rows = await _dbContext.SystemSettings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .Select(s => new { s.Key, s.Value })
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var defaults = PayrollScheduleConfig.Default;

        var rawMode = rows.GetValueOrDefault(KeyMode)?.Trim().ToUpperInvariant();
        var mode = rawMode == "DAYS_BEFORE_MONTH_END"
            ? PayrollPaymentDateMode.DaysBeforeMonthEnd
            : PayrollPaymentDateMode.FixedDay;

        return new PayrollScheduleConfig(
            mode,
            ParseIntOr(rows.GetValueOrDefault(KeyFirstHalfDay), defaults.FirstHalfPaymentDay),
            ParseIntOr(rows.GetValueOrDefault(KeySecondHalfDay), defaults.SecondHalfPaymentDay),
            ParseIntOr(rows.GetValueOrDefault(KeyDaysBeforeMonthEnd), defaults.DaysBeforeMonthEnd));
    }

    private static int ParseIntOr(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return int.TryParse(value.Trim(), out var parsed) ? parsed : fallback;
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Payroll;

/// <summary>
/// Reads the owner-configured payment-date policy from the SystemSettings table and computes the
/// authoritative (cutoff, payment) for a period via <see cref="PayrollScheduleCalculator"/>.
/// Missing / blank / non-numeric values fall back to the policy defaults (see
/// <see cref="PayrollScheduleConfig.Default"/>), so the behavior is robust even on a DB whose
/// settings rows were never seeded.
/// </summary>
public sealed class PayrollSchedulePolicy : IPayrollSchedulePolicy
{
    // System-setting keys — the single backend source of truth for these strings.
    private const string KeyMode = "PAYROLL_PAYMENT_DATE_MODE";
    private const string KeyFirstHalfDay = "PAYROLL_FIRST_HALF_PAYMENT_DAY";
    private const string KeySecondHalfDay = "PAYROLL_SECOND_HALF_PAYMENT_DAY";
    private const string KeyDaysBeforeMonthEnd = "PAYROLL_DAYS_BEFORE_MONTH_END";

    private readonly NursingCareDbContext _dbContext;

    public PayrollSchedulePolicy(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(DateOnly Cutoff, DateOnly Payment)> ComputeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        var config = await ReadConfigAsync(cancellationToken);
        return PayrollScheduleCalculator.Compute(start, end, config);
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

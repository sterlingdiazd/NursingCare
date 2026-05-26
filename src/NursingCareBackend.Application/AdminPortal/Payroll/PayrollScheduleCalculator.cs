using System;

namespace NursingCareBackend.Application.AdminPortal.Payroll;

/// <summary>
/// How the payment date is derived for a second-half / full-month period.
/// </summary>
public enum PayrollPaymentDateMode
{
    /// <summary>Pay on a fixed day-of-month (0 for the second half means "last day of month").</summary>
    FixedDay,

    /// <summary>Pay N days before the last day of the month (second half / full month only).</summary>
    DaysBeforeMonthEnd,
}

/// <summary>
/// The owner-configured payroll payment-date policy, read from system settings. Defaults reproduce
/// today's behavior exactly: 1ra quincena pays day 15, 2da quincena pays the last day of the month.
/// </summary>
public sealed record PayrollScheduleConfig(
    PayrollPaymentDateMode Mode,
    int FirstHalfPaymentDay,
    int SecondHalfPaymentDay,
    int DaysBeforeMonthEnd)
{
    /// <summary>Default policy: FIXED_DAY, 1ra → 15, 2da → 0 (último día del mes), 0 days before.</summary>
    public static PayrollScheduleConfig Default { get; } =
        new(PayrollPaymentDateMode.FixedDay, FirstHalfPaymentDay: 15, SecondHalfPaymentDay: 0, DaysBeforeMonthEnd: 0);
}

/// <summary>
/// Pure (DB-free) computation of the authoritative (cutoff, payment) dates for a payroll period.
/// Mirrors the mobile create-period prefill policy, with one deliberate difference required by the
/// owner: a FULL MONTH period (1 → last day) is treated EXACTLY like a 2da quincena (month-end rule),
/// not as a non-standard range. This is what makes the seeded March period (01→31) pay on 31/03.
/// </summary>
public static class PayrollScheduleCalculator
{
    public static (DateOnly Cutoff, DateOnly Payment) Compute(DateOnly start, DateOnly end, PayrollScheduleConfig config)
    {
        config ??= PayrollScheduleConfig.Default;

        var lastDay = DateTime.DaysInMonth(end.Year, end.Month);

        DateOnly payment;
        if (start.Day == 1 && end.Day == 15)
        {
            // 1ra quincena: always its fixed day, even in offset mode.
            payment = new DateOnly(end.Year, end.Month, Clamp(config.FirstHalfPaymentDay, 1, lastDay));
        }
        else if ((start.Day == 16 && end.Day == lastDay) || (start.Day == 1 && end.Day == lastDay))
        {
            // 2da quincena OR mes completo: same month-end rule.
            payment = config.Mode == PayrollPaymentDateMode.DaysBeforeMonthEnd
                ? new DateOnly(end.Year, end.Month, Math.Max(1, lastDay - config.DaysBeforeMonthEnd))
                : new DateOnly(
                    end.Year,
                    end.Month,
                    config.SecondHalfPaymentDay == 0 ? lastDay : Clamp(config.SecondHalfPaymentDay, 1, lastDay));
        }
        else
        {
            // Período no estándar: pay on the end date (original behavior).
            payment = end;
        }

        // A pathological policy could push payment before start; keep it within the period.
        if (payment < start)
        {
            payment = start;
        }

        // Backend invariant: cutoff in [start, end] and payment >= cutoff. Start from end − 2,
        // then pull cutoff back so it never exceeds payment, and clamp to be >= start.
        var cutoff = end.AddDays(-2);
        if (cutoff > payment)
        {
            cutoff = payment;
        }
        if (cutoff < start)
        {
            cutoff = start;
        }

        return (cutoff, payment);
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
}

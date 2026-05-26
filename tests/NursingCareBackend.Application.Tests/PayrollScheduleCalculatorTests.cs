using System;
using NursingCareBackend.Application.AdminPortal.Payroll;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Unit tests for the authoritative payroll payment-date policy computation. These exercise the
/// pure <see cref="PayrollScheduleCalculator"/> with no database dependency; the Infrastructure
/// <c>PayrollSchedulePolicy</c> only adapts SystemSettings into a <see cref="PayrollScheduleConfig"/>
/// and delegates here.
/// </summary>
public sealed class PayrollScheduleCalculatorTests
{
    [Fact]
    public void FirstHalf_DefaultPolicy_PaysOnDay15()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 15);

        var (cutoff, payment) = PayrollScheduleCalculator.Compute(start, end, PayrollScheduleConfig.Default);

        Assert.Equal(new DateOnly(2026, 7, 15), payment);
        // cutoff = min(end − 2, payment) clamped >= start → 13/07.
        Assert.Equal(new DateOnly(2026, 7, 13), cutoff);
    }

    [Fact]
    public void FirstHalf_FixedDayPolicy_PaysOnConfiguredDay()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 15);
        var config = PayrollScheduleConfig.Default with { FirstHalfPaymentDay = 10 };

        var (cutoff, payment) = PayrollScheduleCalculator.Compute(start, end, config);

        Assert.Equal(new DateOnly(2026, 7, 10), payment);
        // payment (10) < end−2 (13) → cutoff pulled back to payment.
        Assert.Equal(new DateOnly(2026, 7, 10), cutoff);
    }

    [Fact]
    public void SecondHalf_DefaultPolicy_PaysOnLastDayOfMonth()
    {
        var start = new DateOnly(2026, 5, 16);
        var end = new DateOnly(2026, 5, 31);

        var (cutoff, payment) = PayrollScheduleCalculator.Compute(start, end, PayrollScheduleConfig.Default);

        Assert.Equal(new DateOnly(2026, 5, 31), payment);
        Assert.Equal(new DateOnly(2026, 5, 29), cutoff);
    }

    [Fact]
    public void SecondHalf_FixedDayPolicy_PaysOnConfiguredDayClampedToMonth()
    {
        var start = new DateOnly(2026, 2, 16);
        var end = new DateOnly(2026, 2, 28); // 2026 is not a leap year → last day 28.
        var config = PayrollScheduleConfig.Default with { SecondHalfPaymentDay = 31 };

        var (_, payment) = PayrollScheduleCalculator.Compute(start, end, config);

        // Day 31 clamped to the month's last day (28).
        Assert.Equal(new DateOnly(2026, 2, 28), payment);
    }

    [Fact]
    public void FullMonth_DefaultPolicy_TreatedAsSecondHalf_PaysOnLastDay()
    {
        // The March case: 2026-03-01 → 2026-03-31 must pay on the last day of the month.
        var start = new DateOnly(2026, 3, 1);
        var end = new DateOnly(2026, 3, 31);

        var (cutoff, payment) = PayrollScheduleCalculator.Compute(start, end, PayrollScheduleConfig.Default);

        Assert.Equal(new DateOnly(2026, 3, 31), payment);
        Assert.Equal(new DateOnly(2026, 3, 29), cutoff);
    }

    [Fact]
    public void SecondHalf_DaysBeforeMonthEnd_PaysLastDayMinusN_WithCutoffPulledBack()
    {
        var start = new DateOnly(2026, 5, 16);
        var end = new DateOnly(2026, 5, 31);
        var config = PayrollScheduleConfig.Default with
        {
            Mode = PayrollPaymentDateMode.DaysBeforeMonthEnd,
            DaysBeforeMonthEnd = 5,
        };

        var (cutoff, payment) = PayrollScheduleCalculator.Compute(start, end, config);

        // lastDay (31) − 5 = 26.
        Assert.Equal(new DateOnly(2026, 5, 26), payment);
        // payment (26) < end−2 (29) → cutoff pulled back to payment (26).
        Assert.Equal(new DateOnly(2026, 5, 26), cutoff);
    }

    [Fact]
    public void FullMonth_DaysBeforeMonthEnd_PaysLastDayMinusN()
    {
        var start = new DateOnly(2026, 3, 1);
        var end = new DateOnly(2026, 3, 31);
        var config = PayrollScheduleConfig.Default with
        {
            Mode = PayrollPaymentDateMode.DaysBeforeMonthEnd,
            DaysBeforeMonthEnd = 2,
        };

        var (_, payment) = PayrollScheduleCalculator.Compute(start, end, config);

        Assert.Equal(new DateOnly(2026, 3, 29), payment);
    }

    [Fact]
    public void NonStandardPeriod_PaysOnEndDate()
    {
        // Neither 1ra (1→15), 2da (16→last), nor full month (1→last): falls back to end date.
        var start = new DateOnly(2026, 6, 5);
        var end = new DateOnly(2026, 6, 20);

        var (cutoff, payment) = PayrollScheduleCalculator.Compute(start, end, PayrollScheduleConfig.Default);

        Assert.Equal(new DateOnly(2026, 6, 20), payment);
        Assert.Equal(new DateOnly(2026, 6, 18), cutoff);
    }

    [Fact]
    public void PathologicalDaysBeforeMonthEnd_KeepsPaymentWithinPeriod()
    {
        // A huge offset would push payment before start; it must clamp into [start, end].
        var start = new DateOnly(2026, 5, 16);
        var end = new DateOnly(2026, 5, 31);
        var config = PayrollScheduleConfig.Default with
        {
            Mode = PayrollPaymentDateMode.DaysBeforeMonthEnd,
            DaysBeforeMonthEnd = 60,
        };

        var (cutoff, payment) = PayrollScheduleCalculator.Compute(start, end, config);

        Assert.True(payment >= start && payment <= end);
        Assert.True(cutoff >= start && cutoff <= end);
        Assert.True(payment >= cutoff);
    }
}

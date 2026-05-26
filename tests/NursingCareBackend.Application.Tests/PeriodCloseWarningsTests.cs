using FluentAssertions;
using NursingCareBackend.Application.AdminPortal.Payroll;

namespace NursingCareBackend.Application.Tests;

public sealed class PeriodCloseWarningsTests
{
    [Theory]
    [InlineData(0, 0, 0, false)] // nothing -> no warning, period closes silently
    [InlineData(1, 0, 0, true)]  // negative net
    [InlineData(0, 1, 0, true)]  // unliquidated service
    [InlineData(0, 0, 1, true)]  // unpaid nurse alone must warn (the "nobody got paid" backstop)
    [InlineData(2, 3, 4, true)]
    public void HasWarnings_Is_True_When_Any_Counter_Is_Positive(
        int negativeNet, int unliquidated, int unpaid, bool expected)
    {
        new PeriodCloseWarnings(negativeNet, unliquidated, unpaid).HasWarnings.Should().Be(expected);
    }

    [Fact]
    public void UnpaidNurses_Defaults_To_Zero_For_BackCompat()
    {
        // The legacy 2-arg construction must still compile and not flag an unpaid warning.
        var w = new PeriodCloseWarnings(0, 0);
        w.UnpaidNurses.Should().Be(0);
        w.HasWarnings.Should().BeFalse();
    }
}

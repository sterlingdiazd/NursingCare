using NursingCareBackend.Domain.Payroll;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

public class PayrollPeriodTests
{
    private static readonly DateTime Now = new(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc);

    private static PayrollPeriod StandardSecondHalfMay() =>
        PayrollPeriod.Create(
            new DateOnly(2026, 5, 16),
            new DateOnly(2026, 5, 31),
            new DateOnly(2026, 5, 29),
            new DateOnly(2026, 5, 31),
            Now);

    [Fact]
    public void Create_Standard_Quincena_Is_Open_With_Valid_Schedule()
    {
        var period = StandardSecondHalfMay();

        Assert.Equal(PayrollPeriodStatus.Open, period.Status);
        Assert.False(period.IsClosed);
        Assert.Null(period.ClosedAtUtc);
    }

    [Fact]
    public void Create_Rejects_End_Before_Start()
    {
        Assert.Throws<ArgumentException>(() => PayrollPeriod.Create(
            new DateOnly(2026, 5, 16),
            new DateOnly(2026, 5, 15),
            new DateOnly(2026, 5, 14),
            new DateOnly(2026, 5, 16),
            Now));
    }

    [Fact]
    public void Create_Rejects_Cutoff_Before_Start()
    {
        Assert.Throws<ArgumentException>(() => PayrollPeriod.Create(
            new DateOnly(2026, 5, 16),
            new DateOnly(2026, 5, 31),
            new DateOnly(2026, 5, 15), // before start
            new DateOnly(2026, 5, 31),
            Now));
    }

    [Fact]
    public void Create_Rejects_Cutoff_After_End()
    {
        // Regla de industria: el corte (cierre contable) cae dentro del período, nunca después del fin.
        Assert.Throws<ArgumentException>(() => PayrollPeriod.Create(
            new DateOnly(2026, 5, 16),
            new DateOnly(2026, 5, 31),
            new DateOnly(2026, 6, 1), // after end
            new DateOnly(2026, 6, 1),
            Now));
    }

    [Fact]
    public void Create_Rejects_Payment_Before_Cutoff()
    {
        Assert.Throws<ArgumentException>(() => PayrollPeriod.Create(
            new DateOnly(2026, 5, 16),
            new DateOnly(2026, 5, 31),
            new DateOnly(2026, 5, 29),
            new DateOnly(2026, 5, 28), // before cutoff
            Now));
    }

    [Fact]
    public void Create_Allows_Cutoff_Equal_To_End()
    {
        var period = PayrollPeriod.Create(
            new DateOnly(2026, 5, 16),
            new DateOnly(2026, 5, 31),
            new DateOnly(2026, 5, 31), // cutoff == end is valid
            new DateOnly(2026, 5, 31),
            Now);

        Assert.Equal(new DateOnly(2026, 5, 31), period.CutoffDate);
    }

    [Fact]
    public void Close_Transitions_To_Closed_And_Stamps_Time()
    {
        var period = StandardSecondHalfMay();

        period.Close(Now);

        Assert.True(period.IsClosed);
        Assert.Equal(Now, period.ClosedAtUtc);
    }

    [Fact]
    public void Close_Is_Idempotent()
    {
        var period = StandardSecondHalfMay();
        period.Close(Now);
        var firstClosedAt = period.ClosedAtUtc;

        period.Close(Now.AddDays(1)); // no-op

        Assert.Equal(firstClosedAt, period.ClosedAtUtc);
    }
}

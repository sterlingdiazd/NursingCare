using NursingCareBackend.Domain.Payroll;
using Xunit;

namespace NursingCareBackend.Domain.Tests.Payroll;

public class PayrollPeriodImmutabilityTests
{
    // ── EnsureOpen — core behavior ────────────────────────────────────────────

    [Fact]
    public void EnsureOpen_WhenOpen_DoesNotThrow()
    {
        var period = CreateOpenPeriod();

        var act = () => period.EnsureOpen();

        var exception = Record.Exception(act);
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureOpen_WhenClosed_ThrowsPayrollPeriodClosedException()
    {
        var period = CreateOpenPeriod();
        period.Close(DateTime.UtcNow);

        var act = () => period.EnsureOpen();

        Assert.Throws<PayrollPeriodClosedException>(act);
    }

    [Fact]
    public void EnsureOpen_WhenClosed_ExceptionMessageContainsPeriodId()
    {
        var period = CreateOpenPeriod();
        period.Close(DateTime.UtcNow);

        var ex = Assert.Throws<PayrollPeriodClosedException>(() => period.EnsureOpen());

        Assert.Contains(period.Id.ToString(), ex.Message);
    }

    [Fact]
    public void IsClosed_WhenOpen_ReturnsFalse()
    {
        var period = CreateOpenPeriod();

        Assert.False(period.IsClosed);
    }

    [Fact]
    public void IsClosed_WhenClosed_ReturnsTrue()
    {
        var period = CreateOpenPeriod();
        period.Close(DateTime.UtcNow);

        Assert.True(period.IsClosed);
    }

    // ── PayrollPeriodClosedException constructors ─────────────────────────────

    [Fact]
    public void PayrollPeriodClosedException_DefaultConstructor_HasMessage()
    {
        var ex = new PayrollPeriodClosedException();

        Assert.NotNull(ex.Message);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void PayrollPeriodClosedException_WithPeriodId_MessageContainsId()
    {
        var periodId = Guid.NewGuid();

        var ex = new PayrollPeriodClosedException(periodId);

        Assert.Contains(periodId.ToString(), ex.Message);
    }

    [Fact]
    public void PayrollPeriodClosedException_IsInvalidOperationException()
    {
        var ex = new PayrollPeriodClosedException();

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    // ── Close idempotency does not affect EnsureOpen ──────────────────────────

    [Fact]
    public void Close_CalledTwice_PeriodRemainsClosedAndEnsureOpenThrows()
    {
        var period = CreateOpenPeriod();
        var closedAt = DateTime.UtcNow;
        period.Close(closedAt);
        period.Close(closedAt.AddHours(1)); // second call is idempotent

        Assert.Throws<PayrollPeriodClosedException>(() => period.EnsureOpen());
        // Verify original close timestamp is preserved (second Close call must not overwrite it)
        Assert.Equal(closedAt, period.ClosedAtUtc);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static PayrollPeriod CreateOpenPeriod()
    {
        var start = new DateOnly(2030, 1, 1);
        var end = new DateOnly(2030, 1, 15);
        return PayrollPeriod.Create(
            startDate: start,
            endDate: end,
            cutoffDate: end.AddDays(-2),
            paymentDate: end,
            createdAtUtc: DateTime.UtcNow);
    }
}

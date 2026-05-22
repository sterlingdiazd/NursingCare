using NursingCareBackend.Domain.Payroll;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

public class ScheduledDeductionTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Start = new(2026, 5, 1);

    private static ScheduledDeduction Loan(decimal principal, decimal rate, int installments) =>
        ScheduledDeduction.CreateAmortizing(
            Guid.NewGuid(), DeductionType.Loan, "Préstamo",
            principal, rate, installments, DeductionCadence.PerPeriod, Start, null, Guid.NewGuid(), Now);

    [Fact]
    public void CreateAmortizing_Applies_Simple_Interest_On_Principal()
    {
        var loan = Loan(6000m, 10m, 6);

        Assert.Equal(6600m, loan.TotalRepayable);   // 6000 * 1.10
        Assert.Equal(1100m, loan.InstallmentAmount); // 6600 / 6
        Assert.Equal(6600m, loan.RemainingBalance);
        Assert.Equal(ScheduledDeductionStatus.Active, loan.Status);
    }

    [Fact]
    public void Installment_Schedule_Sums_Exactly_To_TotalRepayable()
    {
        var loan = Loan(100m, 0m, 3); // total 100, installment ceils to 33.34

        decimal scheduled = 0m;
        var amounts = new List<decimal>();
        for (var count = 0; loan.AppliesToPeriod(Start, Start.AddDays(14), count, scheduled); count++)
        {
            var (_, amount) = loan.NextInstallment(count, scheduled);
            amounts.Add(amount);
            scheduled += amount;
            if (count > 10) break; // safety
        }

        Assert.Equal(3, amounts.Count);
        Assert.Equal(100m, amounts.Sum());
        Assert.Equal(33.34m, amounts[0]);
        Assert.Equal(33.32m, amounts[^1]); // final absorbs the smaller remainder
    }

    [Fact]
    public void Monthly_Cadence_Only_Applies_In_Month_Closing_Half()
    {
        var loan = Loan(1000m, 0m, 4);
        loan = ScheduledDeduction.CreateAmortizing(
            Guid.NewGuid(), DeductionType.Loan, "Préstamo", 1000m, 0m, 4,
            DeductionCadence.Monthly, new DateOnly(2026, 5, 1), null, Guid.NewGuid(), Now);

        // First-half period (1-15): no installment.
        Assert.False(loan.AppliesToPeriod(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 15), 0, 0m));
        // Second-half period (16-end): installment due.
        Assert.True(loan.AppliesToPeriod(new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 31), 0, 0m));
    }

    [Fact]
    public void Does_Not_Apply_Before_Start_Period()
    {
        var loan = ScheduledDeduction.CreateAmortizing(
            Guid.NewGuid(), DeductionType.Loan, "Préstamo", 1000m, 0m, 4,
            DeductionCadence.PerPeriod, new DateOnly(2026, 6, 1), null, Guid.NewGuid(), Now);

        Assert.False(loan.AppliesToPeriod(new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 31), 0, 0m));
        Assert.True(loan.AppliesToPeriod(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 15), 0, 0m));
    }

    [Fact]
    public void ApplySettlement_Completes_Loan_When_Fully_Paid()
    {
        var loan = Loan(2000m, 0m, 2); // total 2000, installment 1000

        loan.ApplySettlement(1, 1000m, Now);
        Assert.Equal(ScheduledDeductionStatus.Active, loan.Status);
        Assert.Equal(1000m, loan.RemainingBalance);

        loan.ApplySettlement(2, 2000m, Now);
        Assert.Equal(ScheduledDeductionStatus.Completed, loan.Status);
        Assert.Equal(0m, loan.RemainingBalance);
        Assert.NotNull(loan.ClosedAtUtc);
    }

    [Fact]
    public void Recurring_Open_Ended_Keeps_Applying_And_Never_Auto_Completes()
    {
        var seguro = ScheduledDeduction.CreateRecurring(
            Guid.NewGuid(), DeductionType.Insurance, "Seguro Médico", 500m,
            DeductionCadence.Monthly, Start, endDate: null, maxOccurrences: null, null, Guid.NewGuid(), Now);

        Assert.True(seguro.AppliesToPeriod(new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 31), 12, 0m));
        var (_, amount) = seguro.NextInstallment(12, 0m);
        Assert.Equal(500m, amount);

        seguro.ApplySettlement(12, 6000m, Now);
        Assert.Equal(ScheduledDeductionStatus.Active, seguro.Status); // open-ended: stays active
    }

    [Fact]
    public void Recurring_With_MaxOccurrences_Stops_And_Completes()
    {
        var plan = ScheduledDeduction.CreateRecurring(
            Guid.NewGuid(), DeductionType.Other, "Cuota club", 200m,
            DeductionCadence.PerPeriod, Start, endDate: null, maxOccurrences: 3, null, Guid.NewGuid(), Now);

        Assert.True(plan.AppliesToPeriod(Start, Start.AddDays(14), 2, 0m));
        Assert.False(plan.AppliesToPeriod(Start, Start.AddDays(14), 3, 0m)); // reached max

        plan.ApplySettlement(3, 600m, Now);
        Assert.Equal(ScheduledDeductionStatus.Completed, plan.Status);
    }

    [Fact]
    public void Cancel_Sets_Status_Reason_And_ClosedAt()
    {
        var loan = Loan(1000m, 0m, 4);
        var admin = Guid.NewGuid();

        loan.Cancel(admin, "Acuerdo con la enfermera", Now);

        Assert.Equal(ScheduledDeductionStatus.Cancelled, loan.Status);
        Assert.Equal("Acuerdo con la enfermera", loan.CancelReason);
        Assert.Equal(admin, loan.CancelledByUserId);
        Assert.NotNull(loan.ClosedAtUtc);
    }

    [Fact]
    public void Skipped_Installment_Is_Recovered_By_A_Later_Period()
    {
        var loan = Loan(900m, 0m, 3); // total 900, installment 300

        // Two installments scheduled, then one "skipped" (0), so scheduledSoFar lags the total.
        // The plan must keep applying until the full 900 has been allotted.
        decimal scheduled = 300m + 0m; // one paid 300, one skipped 0
        Assert.True(loan.AppliesToPeriod(Start, Start.AddDays(14), 2, scheduled));

        var (_, amount) = loan.NextInstallment(2, scheduled);
        Assert.Equal(300m, amount); // still owed 600, capped at installment 300
    }
}

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Payroll;

public sealed class ScheduledDeductionService : IScheduledDeductionService
{
    private readonly NursingCareDbContext _dbContext;

    public ScheduledDeductionService(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureInstallmentsForOpenPeriodsAsync(CancellationToken cancellationToken)
    {
        var openPeriods = await _dbContext.PayrollPeriods
            .Where(p => p.Status == PayrollPeriodStatus.Open)
            .OrderBy(p => p.StartDate)
            .ToListAsync(cancellationToken);

        if (openPeriods.Count == 0) return;

        var actives = await _dbContext.ScheduledDeductions
            .Where(s => s.Status == ScheduledDeductionStatus.Active)
            .ToListAsync(cancellationToken);

        if (actives.Count == 0) return;

        var activeIds = actives.Select(s => s.Id).ToList();
        var existing = await _dbContext.DeductionRecords
            .Where(d => d.ScheduledDeductionId != null && activeIds.Contains(d.ScheduledDeductionId.Value))
            .Select(d => new { ScheduledDeductionId = d.ScheduledDeductionId!.Value, d.PayrollPeriodId, d.Amount })
            .ToListAsync(cancellationToken);

        var generatedPairs = new HashSet<(Guid Plan, Guid? Period)>(
            existing.Select(e => (e.ScheduledDeductionId, e.PayrollPeriodId)));

        // Authoritative per-plan progress already on record: installment count and amount scheduled.
        var generatedCount = existing
            .GroupBy(e => e.ScheduledDeductionId)
            .ToDictionary(g => g.Key, g => g.Count());
        var scheduledAmount = existing
            .GroupBy(e => e.ScheduledDeductionId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var now = DateTime.UtcNow;
        var touched = new HashSet<Guid>();

        // Periods are processed chronologically so installment sequence numbers stay ordered.
        foreach (var period in openPeriods)
        {
            foreach (var plan in actives)
            {
                if (generatedPairs.Contains((plan.Id, period.Id))) continue;

                var count = generatedCount.GetValueOrDefault(plan.Id, 0);
                var scheduledSoFar = scheduledAmount.GetValueOrDefault(plan.Id, 0m);
                if (!plan.AppliesToPeriod(period.StartDate, period.EndDate, count, scheduledSoFar)) continue;

                var (sequence, amount) = plan.NextInstallment(count, scheduledSoFar);
                _dbContext.DeductionRecords.Add(DeductionRecord.Create(
                    nurseUserId: plan.NurseUserId,
                    payrollPeriodId: period.Id,
                    deductionType: plan.DeductionType,
                    label: BuildInstallmentLabel(plan, sequence),
                    amount: amount,
                    notes: null,
                    effectiveAtUtc: period.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    createdAtUtc: now,
                    scheduledDeductionId: plan.Id,
                    installmentSequence: sequence));

                generatedCount[plan.Id] = count + 1;
                scheduledAmount[plan.Id] = scheduledSoFar + amount;
                generatedPairs.Add((plan.Id, period.Id));
                touched.Add(plan.Id);
            }
        }

        foreach (var plan in actives.Where(p => touched.Contains(p.Id)))
        {
            plan.SyncGeneratedCount(generatedCount[plan.Id]);
        }

        if (touched.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SettlePeriodInstallmentsAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var planIds = await _dbContext.DeductionRecords
            .Where(d => d.PayrollPeriodId == periodId && d.ScheduledDeductionId != null)
            .Select(d => d.ScheduledDeductionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (planIds.Count == 0) return;

        var closedPeriodIds = await _dbContext.PayrollPeriods
            .Where(p => p.Status == PayrollPeriodStatus.Closed)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // Sum each plan's installments that live in already-closed periods (authoritative "paid").
        var settledByPlan = await _dbContext.DeductionRecords
            .Where(d => d.ScheduledDeductionId != null
                && planIds.Contains(d.ScheduledDeductionId.Value)
                && d.PayrollPeriodId != null
                && closedPeriodIds.Contains(d.PayrollPeriodId.Value))
            .GroupBy(d => d.ScheduledDeductionId!.Value)
            .Select(g => new { PlanId = g.Key, Count = g.Count(), Total = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(x => x.PlanId, x => (x.Count, x.Total), cancellationToken);

        var plans = await _dbContext.ScheduledDeductions
            .Where(s => planIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var plan in plans)
        {
            var settled = settledByPlan.GetValueOrDefault(plan.Id, (Count: 0, Total: 0m));
            plan.ApplySettlement(settled.Count, settled.Total, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildInstallmentLabel(ScheduledDeduction plan, int sequence) =>
        plan.Modality == ScheduleModality.Amortizing
            ? $"{plan.Label} · cuota {sequence} de {plan.TotalInstallments}"
            : plan.Label;
}

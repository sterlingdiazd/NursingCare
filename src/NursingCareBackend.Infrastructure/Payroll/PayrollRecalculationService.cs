using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Payroll;

public sealed class PayrollRecalculationService : IPayrollRecalculationService
{
    private readonly NursingCareDbContext _dbContext;

    public PayrollRecalculationService(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RecalculatePayrollResult> RecalculateAsync(
        Guid triggeredByUserId,
        RecalculatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Load open periods matching optional periodId filter
        var periodsQuery = _dbContext.PayrollPeriods
            .Where(p => p.Status == PayrollPeriodStatus.Open);

        if (request.PeriodId.HasValue)
            periodsQuery = periodsQuery.Where(p => p.Id == request.PeriodId.Value);

        var openPeriodIds = await periodsQuery
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (openPeriodIds.Count == 0)
        {
            var emptyAudit = PayrollRecalculationAudit.Create(triggeredByUserId, now, request.PeriodId, request.RuleId, 0, 0m, 0m);
            _dbContext.PayrollRecalculationAudits.Add(emptyAudit);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new RecalculatePayrollResult(emptyAudit.Id, 0, 0m, 0m, now);
        }

        // Load all active compensation rules
        var rules = await _dbContext.CompensationRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

        // If ruleId filter specified, only use that rule
        if (request.RuleId.HasValue)
            rules = rules.Where(r => r.Id == request.RuleId.Value).ToList();

        if (rules.Count == 0)
        {
            var emptyAudit = PayrollRecalculationAudit.Create(triggeredByUserId, now, request.PeriodId, request.RuleId, 0, 0m, 0m);
            _dbContext.PayrollRecalculationAudits.Add(emptyAudit);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new RecalculatePayrollResult(emptyAudit.Id, 0, 0m, 0m, now);
        }

        // Load payroll lines in open periods that have a ServiceExecutionId
        var lines = await _dbContext.PayrollLines
            .Where(l => openPeriodIds.Contains(l.PayrollPeriodId) && l.ServiceExecutionId != null)
            .ToListAsync(cancellationToken);

        var executionIds = lines.Select(l => l.ServiceExecutionId!.Value).Distinct().ToList();

        var executions = await _dbContext.ServiceExecutions
            .Where(e => executionIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        var executionById = executions.ToDictionary(e => e.Id);

        decimal totalOldNet = 0m;
        decimal totalNewNet = 0m;
        int affected = 0;

        foreach (var line in lines)
        {
            if (!executionById.TryGetValue(line.ServiceExecutionId!.Value, out var exec))
                continue;

            var rule = ResolveRule(rules, exec.PricingCategoryCode, exec.UnitType, null);
            if (rule is null) continue;

            // Recompute
            var variantPercent = ResolveVariantPercent(rule, exec.Variant);
            var subtotalBeforeSupplies = exec.SubtotalBeforeSupplies;
            var baseComp = ResolveBaseCompensation(rule, subtotalBeforeSupplies, exec.Unit, variantPercent);
            var transport = decimal.Round(
                subtotalBeforeSupplies
                * Math.Max(0m, exec.DistanceMultiplierSnapshot - 1.0m)
                * (rule.TransportIncentivePercent / 100m), 2, MidpointRounding.AwayFromZero);
            var complexity = decimal.Round(
                subtotalBeforeSupplies
                * Math.Max(0m, exec.ComplexityMultiplierSnapshot - 1.0m)
                * (rule.ComplexityBonusPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var supplies = decimal.Round(exec.MedicalSuppliesCost * (rule.MedicalSuppliesPercent / 100m), 2, MidpointRounding.AwayFromZero);

            var newNet = baseComp + transport + complexity + supplies + line.AdjustmentsTotal - line.DeductionsTotal;
            newNet = decimal.Round(newNet, 2, MidpointRounding.AwayFromZero);

            totalOldNet += line.NetCompensation;
            totalNewNet += newNet;

            line.RefreshAmounts(baseComp, transport, complexity, supplies, line.AdjustmentsTotal, line.DeductionsTotal, now);
            affected++;
        }

        var audit = PayrollRecalculationAudit.Create(triggeredByUserId, now, request.PeriodId, request.RuleId, affected, totalOldNet, totalNewNet);
        _dbContext.PayrollRecalculationAudits.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecalculatePayrollResult(audit.Id, affected, totalOldNet, totalNewNet, now);
    }

    private static CompensationRule? ResolveRule(
        IReadOnlyList<CompensationRule> rules,
        string? pricingCategoryCode,
        string unitTypeCode,
        string? nurseCategoryCode)
    {
        return rules.FirstOrDefault(r =>
            Matches(r.CareRequestCategoryCode, pricingCategoryCode)
            && Matches(r.UnitTypeCode, unitTypeCode)
            && Matches(r.NurseCategoryCode, nurseCategoryCode));
    }

    private static decimal ResolveVariantPercent(CompensationRule rule, ServiceExecutionVariant variant)
        => variant switch
        {
            ServiceExecutionVariant.Partial => rule.PartialServicePercent,
            ServiceExecutionVariant.Express => rule.ExpressServicePercent,
            ServiceExecutionVariant.Suspended => rule.SuspendedServicePercent,
            _ => 100m,
        };

    private static decimal ResolveBaseCompensation(CompensationRule rule, decimal subtotalBeforeSupplies, int unit, decimal variantPercent)
    {
        var percentageAmount = subtotalBeforeSupplies * (rule.BaseCompensationPercent / 100m);
        var fixedAmount = rule.FixedAmountPerUnit * unit;
        var baseAmount = fixedAmount > 0 ? fixedAmount : percentageAmount;
        return decimal.Round(baseAmount * (variantPercent / 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static bool Matches(string? ruleValue, string? currentValue)
        => string.IsNullOrWhiteSpace(ruleValue)
           || string.Equals(ruleValue.Trim(), currentValue?.Trim(), StringComparison.OrdinalIgnoreCase);
}

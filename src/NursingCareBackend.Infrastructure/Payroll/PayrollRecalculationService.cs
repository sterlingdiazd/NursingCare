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

        // SEC-002: If a specific periodId was requested but no matching open period exists,
        // throw so the controller can return 400 instead of a silent HTTP 200 with zero lines.
        if (openPeriodIds.Count == 0 && request.PeriodId.HasValue)
        {
            throw new ArgumentException(
                $"El período '{request.PeriodId.Value}' no existe o no está abierto. Solo se pueden recalcular períodos con estado 'Open'.");
        }

        if (openPeriodIds.Count == 0)
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

        // Nurse pay is decoupled from the client price: pay = nurse rate x days worked.
        var nurseIds = executions.Select(e => e.NurseUserId).Distinct().ToList();
        var nurseRates = await _dbContext.Nurses
            .AsNoTracking()
            .Where(n => nurseIds.Contains(n.UserId))
            .ToDictionaryAsync(
                n => n.UserId,
                n => new NurseRates(n.VisitDailyRate, n.HomeCareMonthlyRate, n.HomeCareMonthlyExpectedDays),
                cancellationToken);

        decimal totalOldNet = 0m;
        decimal totalNewNet = 0m;
        int affected = 0;

        foreach (var line in lines)
        {
            if (!executionById.TryGetValue(line.ServiceExecutionId!.Value, out var exec))
                continue;

            var rates = nurseRates.TryGetValue(exec.NurseUserId, out var r) ? r : new NurseRates(0m, 0m, 30);
            var baseComp = ComputeNurseBasePay(exec.PricingCategoryCode, exec.Unit, rates);

            // Incentives derived from the client price no longer apply to nurse pay.
            var newNet = decimal.Round(baseComp + line.AdjustmentsTotal, 2, MidpointRounding.AwayFromZero);

            totalOldNet += line.NetCompensation;
            totalNewNet += newNet;

            line.RefreshAmounts(baseComp, 0m, 0m, 0m, line.AdjustmentsTotal, 0m, now);
            affected++;
        }

        var audit = PayrollRecalculationAudit.Create(triggeredByUserId, now, request.PeriodId, request.RuleId, affected, totalOldNet, totalNewNet);
        _dbContext.PayrollRecalculationAudits.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecalculatePayrollResult(audit.Id, affected, totalOldNet, totalNewNet, now);
    }

    private readonly record struct NurseRates(decimal VisitDailyRate, decimal HomeCareMonthlyRate, decimal HomeCareMonthlyExpectedDays);

    // Pago de la enfermera = tarifa diaria x dias del servicio, independiente del precio al cliente.
    // Los dias registrados en el servicio (Unit) se vuelven dias pagables al completarse.
    // - Casa hogar: diaria = monto mensual / dias esperados del mes.
    // - Domicilio/medicos: diaria = tarifa por dia (VisitDailyRate).
    private static decimal ComputeNurseBasePay(string? pricingCategoryCode, int days, NurseRates rates)
    {
        var isHogar = string.Equals(pricingCategoryCode, "hogar", StringComparison.OrdinalIgnoreCase);
        var daily = isHogar
            ? (rates.HomeCareMonthlyExpectedDays > 0 ? rates.HomeCareMonthlyRate / rates.HomeCareMonthlyExpectedDays : 0m)
            : rates.VisitDailyRate;
        return decimal.Round(daily * Math.Max(1, days), 2, MidpointRounding.AwayFromZero);
    }
}

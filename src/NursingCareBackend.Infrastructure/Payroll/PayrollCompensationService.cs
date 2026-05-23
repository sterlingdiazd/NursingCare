using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Payroll;

public sealed class PayrollCompensationService : IPayrollCompensationService
{
    private readonly NursingCareDbContext _dbContext;

    public PayrollCompensationService(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordExecutionForCompletedCareRequestAsync(CareRequest careRequest, CancellationToken cancellationToken)
    {
        if (careRequest.Status != CareRequestStatus.Completed || !careRequest.AssignedNurse.HasValue || !careRequest.CompletedAtUtc.HasValue)
        {
            return;
        }

        var nurse = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == careRequest.AssignedNurse.Value)
            .Select(user => new
            {
                user.Id,
                user.Name,
                user.LastName,
                NurseCategory = user.NurseProfile != null ? user.NurseProfile.Category : null,
                VisitDailyRate = user.NurseProfile != null ? user.NurseProfile.VisitDailyRate : 0m,
                HomeCareMonthlyRate = user.NurseProfile != null ? user.NurseProfile.HomeCareMonthlyRate : 0m,
                HomeCareMonthlyExpectedDays = user.NurseProfile != null ? user.NurseProfile.HomeCareMonthlyExpectedDays : 23.83m,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (nurse is null)
        {
            return;
        }

        var subtotalBeforeSupplies = decimal.Round(
            Math.Max(0m, careRequest.Total - (careRequest.MedicalSuppliesCost ?? 0m)),
            2,
            MidpointRounding.AwayFromZero);

        var rule = await ResolveCompensationRuleAsync(
            careRequest.PricingCategoryCode,
            careRequest.UnitType,
            nurse.NurseCategory,
            cancellationToken);

        var executionDate = DateOnly.FromDateTime(careRequest.CompletedAtUtc.Value);
        var payrollPeriod = await GetOrCreatePayrollPeriodAsync(executionDate, careRequest.CompletedAtUtc.Value, cancellationToken);

        var variant = ServiceExecutionVariant.Standard;
        var variantPercent = ResolveVariantPercent(rule, variant);

        // Nurse pay is decoupled from the client price: pay = nurse rate x days worked.
        // The compensation rule is still resolved for the audit snapshot, but does NOT drive pay.
        var baseCompensation = ComputeNurseBasePay(
            careRequest.PricingCategoryCode,
            careRequest.Unit,
            nurse.VisitDailyRate,
            nurse.HomeCareMonthlyRate,
            nurse.HomeCareMonthlyExpectedDays);
        var transportIncentive = 0m;
        var complexityBonus = 0m;
        var medicalSuppliesCompensation = 0m;

        var existingExecution = await _dbContext.ServiceExecutions
            .FirstOrDefaultAsync(item => item.CareRequestId == careRequest.Id, cancellationToken);

        if (existingExecution is null)
        {
            existingExecution = ServiceExecution.Create(
                careRequestId: careRequest.Id,
                nurseUserId: careRequest.AssignedNurse.Value,
                shiftRecordId: null,
                compensationRuleId: rule.Id,
                employmentType: rule.EmploymentType,
                variant: variant,
                executedAtUtc: careRequest.CompletedAtUtc.Value,
                careRequestType: careRequest.CareRequestType,
                unitType: careRequest.UnitType,
                unit: careRequest.Unit,
                pricingCategoryCode: careRequest.PricingCategoryCode,
                distanceFactorCode: careRequest.DistanceFactor,
                complexityLevelCode: careRequest.ComplexityLevel,
                basePrice: careRequest.Price,
                careRequestTotal: careRequest.Total,
                clientBasePrice: careRequest.ClientBasePrice ?? 0m,
                categoryFactorSnapshot: careRequest.CategoryFactorSnapshot ?? 1.0m,
                distanceMultiplierSnapshot: careRequest.DistanceFactorMultiplierSnapshot ?? 1.0m,
                complexityMultiplierSnapshot: careRequest.ComplexityMultiplierSnapshot ?? 1.0m,
                volumeDiscountPercentSnapshot: careRequest.VolumeDiscountPercentSnapshot ?? 0,
                subtotalBeforeSupplies: subtotalBeforeSupplies,
                medicalSuppliesCost: careRequest.MedicalSuppliesCost ?? 0m,
                ruleBaseCompensationPercent: rule.BaseCompensationPercent,
                ruleFixedAmountPerUnit: rule.FixedAmountPerUnit,
                ruleTransportIncentivePercent: rule.TransportIncentivePercent,
                ruleComplexityBonusPercent: rule.ComplexityBonusPercent,
                ruleMedicalSuppliesPercent: rule.MedicalSuppliesPercent,
                ruleVariantPercent: variantPercent,
                baseCompensation: baseCompensation,
                transportIncentive: transportIncentive,
                complexityBonus: complexityBonus,
                medicalSuppliesCompensation: medicalSuppliesCompensation,
                adjustmentsTotal: 0m,
                deductionsTotal: 0m,
                manualOverrideAmount: null,
                notes: "Auto-generado desde la solicitud completada usando el snapshot comercial.",
                createdAtUtc: careRequest.CompletedAtUtc.Value);

            _dbContext.ServiceExecutions.Add(existingExecution);
        }
        else
        {
            existingExecution.Refresh(
                compensationRuleId: rule.Id,
                employmentType: rule.EmploymentType,
                variant: variant,
                executedAtUtc: careRequest.CompletedAtUtc.Value,
                ruleBaseCompensationPercent: rule.BaseCompensationPercent,
                ruleFixedAmountPerUnit: rule.FixedAmountPerUnit,
                ruleTransportIncentivePercent: rule.TransportIncentivePercent,
                ruleComplexityBonusPercent: rule.ComplexityBonusPercent,
                ruleMedicalSuppliesPercent: rule.MedicalSuppliesPercent,
                ruleVariantPercent: variantPercent,
                baseCompensation: baseCompensation,
                transportIncentive: transportIncentive,
                complexityBonus: complexityBonus,
                medicalSuppliesCompensation: medicalSuppliesCompensation,
                adjustmentsTotal: 0m,
                deductionsTotal: 0m,
                manualOverrideAmount: null,
                notes: "Recalculado desde el snapshot comercial persistido.",
                updatedAtUtc: careRequest.CompletedAtUtc.Value);
        }

        var existingPayrollLine = await _dbContext.PayrollLines
            .FirstOrDefaultAsync(line => line.ServiceExecutionId == existingExecution.Id, cancellationToken);

        // Use the catalog display name (human-readable) for the line, not the raw type code.
        var serviceLabel = await _dbContext.CareRequestTypeCatalogs
            .AsNoTracking()
            .Where(t => t.Code == careRequest.CareRequestType)
            .Select(t => t.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(serviceLabel))
        {
            serviceLabel = careRequest.CareRequestType;
        }
        var description = $"{serviceLabel} · solicitud {careRequest.Id}";
        if (existingPayrollLine is null)
        {
            existingPayrollLine = PayrollLine.Create(
                payrollPeriodId: payrollPeriod.Id,
                nurseUserId: careRequest.AssignedNurse.Value,
                serviceExecutionId: existingExecution.Id,
                description: description,
                baseCompensation: existingExecution.BaseCompensation,
                transportIncentive: existingExecution.TransportIncentive,
                complexityBonus: existingExecution.ComplexityBonus,
                medicalSuppliesCompensation: existingExecution.MedicalSuppliesCompensation,
                adjustmentsTotal: existingExecution.AdjustmentsTotal,
                deductionsTotal: existingExecution.DeductionsTotal,
                createdAtUtc: careRequest.CompletedAtUtc.Value);

            _dbContext.PayrollLines.Add(existingPayrollLine);
        }
        else
        {
            existingPayrollLine.RefreshAmounts(
                baseCompensation: existingExecution.BaseCompensation,
                transportIncentive: existingExecution.TransportIncentive,
                complexityBonus: existingExecution.ComplexityBonus,
                medicalSuppliesCompensation: existingExecution.MedicalSuppliesCompensation,
                adjustmentsTotal: existingExecution.AdjustmentsTotal,
                deductionsTotal: existingExecution.DeductionsTotal,
                updatedAtUtc: careRequest.CompletedAtUtc.Value);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CompensationRule> ResolveCompensationRuleAsync(
        string? pricingCategoryCode,
        string unitTypeCode,
        string? nurseCategoryCode,
        CancellationToken cancellationToken)
    {
        var rules = await _dbContext.CompensationRules
            .AsNoTracking()
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.Priority)
            .ToListAsync(cancellationToken);

        var matchingRule = rules.FirstOrDefault(rule =>
            Matches(rule.CareRequestCategoryCode, pricingCategoryCode)
            && Matches(rule.UnitTypeCode, unitTypeCode)
            && Matches(rule.NurseCategoryCode, nurseCategoryCode));

        if (matchingRule is not null)
        {
            return matchingRule;
        }

        return CompensationRule.Create(
            name: "Fallback per-service",
            employmentType: CompensationEmploymentType.PerService,
            careRequestCategoryCode: pricingCategoryCode,
            unitTypeCode: unitTypeCode,
            nurseCategoryCode: nurseCategoryCode,
            baseCompensationPercent: 55m,
            fixedAmountPerUnit: 0m,
            transportIncentivePercent: 15m,
            complexityBonusPercent: 20m,
            medicalSuppliesPercent: 0m,
            partialServicePercent: 65m,
            expressServicePercent: 125m,
            suspendedServicePercent: 40m,
            isActive: true,
            priority: 9999,
            createdAtUtc: DateTime.UtcNow);
    }

    private async Task<PayrollPeriod> GetOrCreatePayrollPeriodAsync(DateOnly serviceDate, DateTime createdAtUtc, CancellationToken cancellationToken)
    {
        var startDate = serviceDate.Day <= 15
            ? new DateOnly(serviceDate.Year, serviceDate.Month, 1)
            : new DateOnly(serviceDate.Year, serviceDate.Month, 16);
        var endDate = serviceDate.Day <= 15
            ? new DateOnly(serviceDate.Year, serviceDate.Month, 15)
            : new DateOnly(serviceDate.Year, serviceDate.Month, DateTime.DaysInMonth(serviceDate.Year, serviceDate.Month));
        var cutoffDate = endDate.AddDays(-2);
        var paymentDate = endDate;

        var existing = await _dbContext.PayrollPeriods
            .FirstOrDefaultAsync(period => period.StartDate == startDate && period.EndDate == endDate, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var created = PayrollPeriod.Create(startDate, endDate, cutoffDate, paymentDate, createdAtUtc);
        _dbContext.PayrollPeriods.Add(created);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    private static decimal ResolveVariantPercent(CompensationRule rule, ServiceExecutionVariant variant)
        => variant switch
        {
            ServiceExecutionVariant.Partial => rule.PartialServicePercent,
            ServiceExecutionVariant.Express => rule.ExpressServicePercent,
            ServiceExecutionVariant.Suspended => rule.SuspendedServicePercent,
            _ => 100m,
        };

    // Pago de la enfermera = tarifa diaria x dias del servicio, independiente del precio al cliente.
    // Los dias registrados en el servicio (Unit) se vuelven dias pagables al completarse.
    // - Casa hogar: diaria = monto mensual / dias esperados del mes.
    // - Domicilio/medicos: diaria = tarifa por dia (VisitDailyRate).
    private static decimal ComputeNurseBasePay(
        string? pricingCategoryCode,
        int days,
        decimal visitDailyRate,
        decimal homeCareMonthlyRate,
        decimal homeCareMonthlyExpectedDays)
    {
        var isHogar = string.Equals(pricingCategoryCode, "hogar", StringComparison.OrdinalIgnoreCase);
        var daily = isHogar
            ? (homeCareMonthlyExpectedDays > 0 ? homeCareMonthlyRate / homeCareMonthlyExpectedDays : 0m)
            : visitDailyRate;
        return decimal.Round(daily * Math.Max(1, days), 2, MidpointRounding.AwayFromZero);
    }

    private static bool Matches(string? ruleValue, string? currentValue)
        => string.IsNullOrWhiteSpace(ruleValue)
           || string.Equals(ruleValue.Trim(), currentValue?.Trim(), StringComparison.OrdinalIgnoreCase);
}

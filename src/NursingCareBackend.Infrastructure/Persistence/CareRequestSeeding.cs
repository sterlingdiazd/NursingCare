using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Infrastructure.Persistence;

/// <summary>
/// Seeds care requests with specific totals for each nurse to test payroll calculations.
/// </summary>
public static class CareRequestSeeding
{
    public static async Task EnsureSeededAsync(NursingCareDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.CareRequests.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var seedPeriodStart = new DateOnly(now.Year, now.Month, 1);
        var seedPeriodEnd = new DateOnly(now.Year, now.Month, 15);
        var createdAtUtc = now.AddDays(-5);

        var payrollPeriod = PayrollPeriod.Create(
            startDate: seedPeriodStart,
            endDate: seedPeriodEnd,
            cutoffDate: seedPeriodEnd.AddDays(-2),
            paymentDate: seedPeriodEnd,
            createdAtUtc: createdAtUtc);
        db.PayrollPeriods.Add(payrollPeriod);
        await db.SaveChangesAsync(cancellationToken);

        var careRequests = new[]
        {
            CreateCareRequest(CatalogSeeding.NurseIds["Lorea"], CatalogSeeding.TestClientId, 12200m, "hogar_diario", "dia_completo", 5, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Charleny"], CatalogSeeding.TestClientId, 12200m, "hogar_diario", "dia_completo", 5, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Valentin"], CatalogSeeding.TestClientId, 13000m, "hogar_diario", "dia_completo", 5, "media", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Marel"], CatalogSeeding.TestClientId, 12000m, "hogar_diario", "dia_completo", 5, "cercana", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Liliana"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Clari"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Solano"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Angela Maria"], CatalogSeeding.TestClientId, 13000m, "hogar_diario", "dia_completo", 5, "media", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Karen"], CatalogSeeding.TestClientId, 15500m, "hogar_diario", "dia_completo", 6, "cercana", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Cristina"], CatalogSeeding.TestClientId, 15500m, "hogar_diario", "dia_completo", 6, "cercana", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Figueredo"], CatalogSeeding.TestClientId, 23000m, "domicilio_24h", "dia_completo", 7, "media", "moderada", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Annie"], CatalogSeeding.TestClientId, 23000m, "domicilio_24h", "dia_completo", 7, "media", "moderada", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Zoila"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Maria Isabel"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Emilina"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Cindy"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Agustina"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Johanna"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Miranda"], CatalogSeeding.TestClientId, 13200m, "hogar_diario", "dia_completo", 5, "cercana", "moderada", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Miguelina"], CatalogSeeding.TestClientId, 13000m, "hogar_diario", "dia_completo", 5, "media", "estandar", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["Celai"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada", payrollPeriod.Id, createdAtUtc),
            CreateCareRequest(CatalogSeeding.NurseIds["De Los Santos"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada", payrollPeriod.Id, createdAtUtc),
        };

        db.CareRequests.AddRange(careRequests);
        await db.SaveChangesAsync(cancellationToken);

        var rules = await db.CompensationRules.AsNoTracking().ToListAsync(cancellationToken);
        var ruleLookup = rules.ToDictionary(r => r.CareRequestCategoryCode ?? "");

        var payrollLines = new List<PayrollLine>();
        foreach (var cr in careRequests)
        {
            var rule = ruleLookup.GetValueOrDefault(cr.PricingCategoryCode ?? "") ?? rules.First();
            var executedAt = cr.CompletedAtUtc!.Value;

            var subtotalBeforeSupplies = Math.Max(0m, cr.Total - (cr.MedicalSuppliesCost ?? 0m));
            var baseComp = decimal.Round(subtotalBeforeSupplies * (rule.BaseCompensationPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var transport = decimal.Round(subtotalBeforeSupplies * Math.Max(0m, (cr.DistanceFactorMultiplierSnapshot ?? 1m) - 1m) * (rule.TransportIncentivePercent / 100m), 2, MidpointRounding.AwayFromZero);
            var complexity = decimal.Round(subtotalBeforeSupplies * Math.Max(0m, (cr.ComplexityMultiplierSnapshot ?? 1m) - 1m) * (rule.ComplexityBonusPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var supplies = decimal.Round((cr.MedicalSuppliesCost ?? 0m) * (rule.MedicalSuppliesPercent / 100m), 2, MidpointRounding.AwayFromZero);

            payrollLines.Add(PayrollLine.Create(
                payrollPeriodId: payrollPeriod.Id,
                nurseUserId: cr.AssignedNurse!.Value,
                serviceExecutionId: null,
                description: $"Servicio {cr.CareRequestType}",
                baseCompensation: baseComp,
                transportIncentive: transport,
                complexityBonus: complexity,
                medicalSuppliesCompensation: supplies,
                adjustmentsTotal: 0m,
                deductionsTotal: 0m,
                createdAtUtc: executedAt));
        }

        db.PayrollLines.AddRange(payrollLines);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CareRequest CreateCareRequest(
        Guid nurseId,
        Guid clientId,
        decimal targetTotal,
        string careRequestTypeCode,
        string unitTypeCode,
        int units,
        string distanceFactorCode,
        string complexityLevelCode,
        Guid payrollPeriodId,
        DateTime createdAtUtc)
    {
        // Mapping of care request type codes to base prices
        var basePrices = new Dictionary<string, decimal>
        {
            { "hogar_diario", 2500m },
            { "hogar_basico", 55000m },
            { "hogar_estandar", 60000m },
            { "hogar_premium", 65000m },
            { "domicilio_dia_12h", 2500m },
            { "domicilio_noche_12h", 2500m },
            { "domicilio_24h", 3500m },
            { "suero", 2000m },
            { "medicamentos", 2000m },
            { "sonda_vesical", 2000m },
            { "sonda_nasogastrica", 3000m },
            { "sonda_peg", 4000m },
            { "curas", 2000m },
        };

        var distanceMultipliers = new Dictionary<string, decimal>
        {
            { "local", 1.0m },
            { "cercana", 1.1m },
            { "media", 1.2m },
            { "lejana", 1.3m },
        };

        var complexityMultipliers = new Dictionary<string, decimal>
        {
            { "estandar", 1.0m },
            { "moderada", 1.1m },
            { "alta", 1.2m },
            { "critica", 1.3m },
        };

        var categoryFactors = new Dictionary<string, decimal>
        {
            { "hogar", 1.0m },
            { "domicilio", 1.2m },
            { "medicos", 1.5m },
        };

        var basePrice = basePrices[careRequestTypeCode];
        var distanceMultiplier = distanceMultipliers[distanceFactorCode];
        var complexityMultiplier = complexityMultipliers[complexityLevelCode];

        // Determine pricing category based on care request type
        string pricingCategory = careRequestTypeCode switch
        {
            var s when s.StartsWith("hogar") => "hogar",
            var s when s.StartsWith("domicilio") => "domicilio",
            var s when s.StartsWith("sonda") || s.StartsWith("medicamentos") || s.StartsWith("suero") || s.StartsWith("curas") => "medicos",
            _ => "hogar"
        };

        var categoryFactor = categoryFactors[pricingCategory];

        // Calculate the effective price with all multipliers
        decimal effectivePrice = basePrice * distanceMultiplier * complexityMultiplier * categoryFactor;
        decimal calculatedTotal = decimal.Round(effectivePrice * units, 2, MidpointRounding.AwayFromZero);

        // If calculated total doesn't match target, adjust the unit count
        if (calculatedTotal != targetTotal)
        {
            var calculatedUnits = (int)Math.Round(targetTotal / effectivePrice, 0);
            calculatedTotal = decimal.Round(effectivePrice * calculatedUnits, 2, MidpointRounding.AwayFromZero);
        }

        var executedAtUtc = createdAtUtc.AddHours(12);
        var period = new DateOnly(createdAtUtc.Year, createdAtUtc.Month, createdAtUtc.Day);

        var careRequest = CareRequest.Create(
            userID: clientId,
            description: $"Care service for {careRequestTypeCode}",
            careRequestReason: $"Care request requiring {careRequestTypeCode} service",
            careRequestType: careRequestTypeCode,
            unitType: unitTypeCode,
            suggestedNurse: null,
            assignedNurse: nurseId,
            unit: units,
            price: basePrice,
            total: calculatedTotal,
            clientBasePrice: null,
            distanceFactor: distanceFactorCode,
            complexityLevel: complexityLevelCode,
            medicalSuppliesCost: null,
            careRequestDate: period,
            pricingCategoryCode: pricingCategory,
            categoryFactorSnapshot: categoryFactor,
            distanceFactorMultiplierSnapshot: distanceMultiplier,
            complexityMultiplierSnapshot: complexityMultiplier,
            volumeDiscountPercentSnapshot: 0,
            createdAtUtc: createdAtUtc);

        careRequest.Approve(createdAtUtc);

        careRequest.Complete(executedAtUtc, nurseId);

        return careRequest;
    }
}

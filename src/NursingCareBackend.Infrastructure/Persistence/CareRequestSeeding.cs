using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.CareRequests;

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

        // Define care request configurations with target totals for each nurse
        var careRequests = new[]
        {
            // Lorea - $12,200
            CreateCareRequest(CatalogSeeding.NurseIds["Lorea"], CatalogSeeding.TestClientId, 12200m, "hogar_diario", "dia_completo", 5, "local", "estandar"),

            // Charleny - $12,200
            CreateCareRequest(CatalogSeeding.NurseIds["Charleny"], CatalogSeeding.TestClientId, 12200m, "hogar_diario", "dia_completo", 5, "local", "estandar"),

            // Valentin - $13,000
            CreateCareRequest(CatalogSeeding.NurseIds["Valentin"], CatalogSeeding.TestClientId, 13000m, "hogar_diario", "dia_completo", 5, "media", "estandar"),

            // Marel - $12,000
            CreateCareRequest(CatalogSeeding.NurseIds["Marel"], CatalogSeeding.TestClientId, 12000m, "hogar_diario", "dia_completo", 5, "cercana", "estandar"),

            // Liliana - $15,000
            CreateCareRequest(CatalogSeeding.NurseIds["Liliana"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar"),

            // Clari - $15,000
            CreateCareRequest(CatalogSeeding.NurseIds["Clari"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar"),

            // Solano - $15,000
            CreateCareRequest(CatalogSeeding.NurseIds["Solano"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar"),

            // Angela Maria - $13,000
            CreateCareRequest(CatalogSeeding.NurseIds["Angela Maria"], CatalogSeeding.TestClientId, 13000m, "hogar_diario", "dia_completo", 5, "media", "estandar"),

            // Karen - $15,500
            CreateCareRequest(CatalogSeeding.NurseIds["Karen"], CatalogSeeding.TestClientId, 15500m, "hogar_diario", "dia_completo", 6, "cercana", "estandar"),

            // Cristina - $15,500
            CreateCareRequest(CatalogSeeding.NurseIds["Cristina"], CatalogSeeding.TestClientId, 15500m, "hogar_diario", "dia_completo", 6, "cercana", "estandar"),

            // Figueredo - $23,000
            CreateCareRequest(CatalogSeeding.NurseIds["Figueredo"], CatalogSeeding.TestClientId, 23000m, "domicilio_24h", "dia_completo", 7, "media", "moderada"),

            // Annie - $23,000
            CreateCareRequest(CatalogSeeding.NurseIds["Annie"], CatalogSeeding.TestClientId, 23000m, "domicilio_24h", "dia_completo", 7, "media", "moderada"),

            // Zoila - $12,500
            CreateCareRequest(CatalogSeeding.NurseIds["Zoila"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada"),

            // Maria Isabel - $12,500
            CreateCareRequest(CatalogSeeding.NurseIds["Maria Isabel"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada"),

            // Emilina - $15,000
            CreateCareRequest(CatalogSeeding.NurseIds["Emilina"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar"),

            // Cindy - $15,000
            CreateCareRequest(CatalogSeeding.NurseIds["Cindy"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar"),

            // Agustina - $15,000
            CreateCareRequest(CatalogSeeding.NurseIds["Agustina"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar"),

            // Johanna - $15,000
            CreateCareRequest(CatalogSeeding.NurseIds["Johanna"], CatalogSeeding.TestClientId, 15000m, "hogar_diario", "dia_completo", 6, "local", "estandar"),

            // Miranda - $13,200
            CreateCareRequest(CatalogSeeding.NurseIds["Miranda"], CatalogSeeding.TestClientId, 13200m, "hogar_diario", "dia_completo", 5, "cercana", "moderada"),

            // Miguelina - $13,000
            CreateCareRequest(CatalogSeeding.NurseIds["Miguelina"], CatalogSeeding.TestClientId, 13000m, "hogar_diario", "dia_completo", 5, "media", "estandar"),

            // Celai - $12,500
            CreateCareRequest(CatalogSeeding.NurseIds["Celai"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada"),

            // De Los Santos - $12,500
            CreateCareRequest(CatalogSeeding.NurseIds["De Los Santos"], CatalogSeeding.TestClientId, 12500m, "hogar_diario", "dia_completo", 5, "local", "moderada"),
        };

        db.CareRequests.AddRange(careRequests);
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
        string complexityLevelCode)
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
            careRequestDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            pricingCategoryCode: pricingCategory,
            categoryFactorSnapshot: categoryFactor,
            distanceFactorMultiplierSnapshot: distanceMultiplier,
            complexityMultiplierSnapshot: complexityMultiplier,
            volumeDiscountPercentSnapshot: 0,
            createdAtUtc: DateTime.UtcNow);

        // Approve the care request so it's ready for completion
        careRequest.Approve(DateTime.UtcNow);

        return careRequest;
    }
}

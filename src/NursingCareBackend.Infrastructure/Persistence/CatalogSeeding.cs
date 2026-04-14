using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.Catalogs;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Infrastructure.Persistence;

/// <summary>
/// Bootstraps default catalog rows equivalent to the former hard-coded dictionaries.
/// Used after EnsureCreated in tests and referenced by migrations.
/// </summary>
public static class CatalogSeeding
{
    public static readonly Guid CategoryHogarId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid CategoryDomicilioId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid CategoryMedicosId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    public static async Task EnsureSeededAsync(NursingCareDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.CareRequestCategoryCatalogs.AnyAsync(cancellationToken))
        {
            return;
        }

        db.CareRequestCategoryCatalogs.AddRange(
            new CareRequestCategoryCatalog
            {
                Id = CategoryHogarId,
                Code = "hogar",
                DisplayName = "Hogar",
                CategoryFactor = 1.0m,
                IsActive = true,
                DisplayOrder = 1,
            },
            new CareRequestCategoryCatalog
            {
                Id = CategoryDomicilioId,
                Code = "domicilio",
                DisplayName = "Domicilio",
                CategoryFactor = 1.2m,
                IsActive = true,
                DisplayOrder = 2,
            },
            new CareRequestCategoryCatalog
            {
                Id = CategoryMedicosId,
                Code = "medicos",
                DisplayName = "Medicos",
                CategoryFactor = 1.5m,
                IsActive = true,
                DisplayOrder = 3,
            });

        db.UnitTypeCatalogs.AddRange(
            Unit("20000000-0000-0000-0000-000000000001", "dia_completo", "Dia completo", 1),
            Unit("20000000-0000-0000-0000-000000000002", "mes", "Mes", 2),
            Unit("20000000-0000-0000-0000-000000000003", "medio_dia", "Medio dia", 3),
            Unit("20000000-0000-0000-0000-000000000004", "sesion", "Sesion", 4));

        db.CareRequestTypeCatalogs.AddRange(
            Type("30000000-0000-0000-0000-000000000001", "hogar_diario", "Hogar diario", "hogar", "dia_completo", 2500m, 1),
            Type("30000000-0000-0000-0000-000000000002", "hogar_basico", "Hogar basico", "hogar", "mes", 55000m, 2),
            Type("30000000-0000-0000-0000-000000000003", "hogar_estandar", "Hogar estandar", "hogar", "mes", 60000m, 3),
            Type("30000000-0000-0000-0000-000000000004", "hogar_premium", "Hogar premium", "hogar", "mes", 65000m, 4),
            Type("30000000-0000-0000-0000-000000000005", "domicilio_dia_12h", "Domicilio dia 12h", "domicilio", "medio_dia", 2500m, 5),
            Type("30000000-0000-0000-0000-000000000006", "domicilio_noche_12h", "Domicilio noche 12h", "domicilio", "medio_dia", 2500m, 6),
            Type("30000000-0000-0000-0000-000000000007", "domicilio_24h", "Domicilio 24h", "domicilio", "dia_completo", 3500m, 7),
            Type("30000000-0000-0000-0000-000000000008", "suero", "Suero", "medicos", "sesion", 2000m, 8),
            Type("30000000-0000-0000-0000-000000000009", "medicamentos", "Medicamentos", "medicos", "sesion", 2000m, 9),
            Type("30000000-0000-0000-0000-000000000010", "sonda_vesical", "Sonda vesical", "medicos", "sesion", 2000m, 10),
            Type("30000000-0000-0000-0000-000000000011", "sonda_nasogastrica", "Sonda nasogastrica", "medicos", "sesion", 3000m, 11),
            Type("30000000-0000-0000-0000-000000000012", "sonda_peg", "Sonda PEG", "medicos", "sesion", 4000m, 12),
            Type("30000000-0000-0000-0000-000000000013", "curas", "Curas", "medicos", "sesion", 2000m, 13));

        db.DistanceFactorCatalogs.AddRange(
            Dist("40000000-0000-0000-0000-000000000001", "local", "Local", 1.0m, 1),
            Dist("40000000-0000-0000-0000-000000000002", "cercana", "Cercana", 1.1m, 2),
            Dist("40000000-0000-0000-0000-000000000003", "media", "Media", 1.2m, 3),
            Dist("40000000-0000-0000-0000-000000000004", "lejana", "Lejana", 1.3m, 4));

        db.ComplexityLevelCatalogs.AddRange(
            Comp("50000000-0000-0000-0000-000000000001", "estandar", "Estandar", 1.0m, 1),
            Comp("50000000-0000-0000-0000-000000000002", "moderada", "Moderada", 1.1m, 2),
            Comp("50000000-0000-0000-0000-000000000003", "alta", "Alta", 1.2m, 3),
            Comp("50000000-0000-0000-0000-000000000004", "critica", "Critica", 1.3m, 4));

        db.VolumeDiscountRules.AddRange(
            new VolumeDiscountRule
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000001"),
                MinimumCount = 1,
                DiscountPercent = 0,
                IsActive = true,
                DisplayOrder = 1,
            },
            new VolumeDiscountRule
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000002"),
                MinimumCount = 5,
                DiscountPercent = 5,
                IsActive = true,
                DisplayOrder = 2,
            },
            new VolumeDiscountRule
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000003"),
                MinimumCount = 10,
                DiscountPercent = 10,
                IsActive = true,
                DisplayOrder = 3,
            },
            new VolumeDiscountRule
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000004"),
                MinimumCount = 20,
                DiscountPercent = 15,
                IsActive = true,
                DisplayOrder = 4,
            },
            new VolumeDiscountRule
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000005"),
                MinimumCount = 50,
                DiscountPercent = 20,
                IsActive = true,
                DisplayOrder = 5,
            });

        db.NurseSpecialtyCatalogs.AddRange(
            NurseSpec("70000000-0000-0000-0000-000000000001", "Cuidado de adultos", "Cuidado de adultos", "Adult Care", 1),
            NurseSpec("70000000-0000-0000-0000-000000000002", "Cuidado pediatrico", "Cuidado pediatrico", "Pediatric Care", 2),
            NurseSpec("70000000-0000-0000-0000-000000000003", "Cuidado geriatrico", "Cuidado geriatrico", "Geriatric Care", 3),
            NurseSpec("70000000-0000-0000-0000-000000000004", "Cuidados intensivos", "Cuidados intensivos", "Critical Care", 4),
            NurseSpec("70000000-0000-0000-0000-000000000005", "Atencion domiciliaria", "Atencion domiciliaria", "Home Care", 5));

        db.NurseCategoryCatalogs.AddRange(
            NurseCat("80000000-0000-0000-0000-000000000001", "Junior", "Junior", null, 1),
            NurseCat("80000000-0000-0000-0000-000000000002", "Semisenior", "Semisenior", "Semi Senior", 2),
            NurseCat("80000000-0000-0000-0000-000000000003", "Senior", "Senior", null, 3),
            NurseCat("80000000-0000-0000-0000-000000000004", "Lider", "Lider", "Lead", 4));

        db.CompensationRules.AddRange(
            CompensationRule.Create(
                name: "Pago por servicio hogar",
                employmentType: CompensationEmploymentType.PerService,
                careRequestCategoryCode: "hogar",
                unitTypeCode: null,
                nurseCategoryCode: null,
                baseCompensationPercent: 52m,
                fixedAmountPerUnit: 0m,
                transportIncentivePercent: 0m,
                complexityBonusPercent: 20m,
                medicalSuppliesPercent: 0m,
                partialServicePercent: 65m,
                expressServicePercent: 120m,
                suspendedServicePercent: 40m,
                isActive: true,
                priority: 10,
                createdAtUtc: DateTime.UtcNow),
            CompensationRule.Create(
                name: "Pago por servicio domicilio",
                employmentType: CompensationEmploymentType.PerService,
                careRequestCategoryCode: "domicilio",
                unitTypeCode: null,
                nurseCategoryCode: null,
                baseCompensationPercent: 55m,
                fixedAmountPerUnit: 0m,
                transportIncentivePercent: 15m,
                complexityBonusPercent: 18m,
                medicalSuppliesPercent: 0m,
                partialServicePercent: 65m,
                expressServicePercent: 125m,
                suspendedServicePercent: 40m,
                isActive: true,
                priority: 20,
                createdAtUtc: DateTime.UtcNow),
            CompensationRule.Create(
                name: "Pago por servicio medicos",
                employmentType: CompensationEmploymentType.PerService,
                careRequestCategoryCode: "medicos",
                unitTypeCode: null,
                nurseCategoryCode: null,
                baseCompensationPercent: 50m,
                fixedAmountPerUnit: 0m,
                transportIncentivePercent: 0m,
                complexityBonusPercent: 10m,
                medicalSuppliesPercent: 25m,
                partialServicePercent: 70m,
                expressServicePercent: 120m,
                suspendedServicePercent: 40m,
                isActive: true,
                priority: 30,
                createdAtUtc: DateTime.UtcNow));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static UnitTypeCatalog Unit(string id, string code, string display, int order)
        => new()
        {
            Id = Guid.Parse(id),
            Code = code,
            DisplayName = display,
            IsActive = true,
            DisplayOrder = order,
        };

    private static CareRequestTypeCatalog Type(
        string id,
        string code,
        string display,
        string categoryCode,
        string unitTypeCode,
        decimal basePrice,
        int order)
        => new()
        {
            Id = Guid.Parse(id),
            Code = code,
            DisplayName = display,
            CareRequestCategoryCode = categoryCode,
            UnitTypeCode = unitTypeCode,
            BasePrice = basePrice,
            IsActive = true,
            DisplayOrder = order,
        };

    private static DistanceFactorCatalog Dist(string id, string code, string display, decimal mult, int order)
        => new()
        {
            Id = Guid.Parse(id),
            Code = code,
            DisplayName = display,
            Multiplier = mult,
            IsActive = true,
            DisplayOrder = order,
        };

    private static ComplexityLevelCatalog Comp(string id, string code, string display, decimal mult, int order)
        => new()
        {
            Id = Guid.Parse(id),
            Code = code,
            DisplayName = display,
            Multiplier = mult,
            IsActive = true,
            DisplayOrder = order,
        };

    private static NurseSpecialtyCatalog NurseSpec(string id, string code, string display, string? alt, int order)
        => new()
        {
            Id = Guid.Parse(id),
            Code = code,
            DisplayName = display,
            AlternativeCodes = alt,
            IsActive = true,
            DisplayOrder = order,
        };

    private static NurseCategoryCatalog NurseCat(string id, string code, string display, string? alt, int order)
        => new()
        {
            Id = Guid.Parse(id),
            Code = code,
            DisplayName = display,
            AlternativeCodes = alt,
            IsActive = true,
            DisplayOrder = order,
        };
}

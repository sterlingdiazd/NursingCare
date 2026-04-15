using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.Catalogs;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Infrastructure.Persistence;

/// <summary>
/// Bootstraps default catalog rows equivalent to the former hard-coded dictionaries.
/// Also seeds fixture users, nurses, and clients for testing.
/// Used after EnsureCreated in tests and referenced by migrations.
/// </summary>
public static class CatalogSeeding
{
    public static readonly Guid SeededAdminId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    public const string SeededAdminEmail = "sterlingdiazd@gmail.com";
    public const string SeededAdminPassword = "12345678";
    public const string SeededAdminName = "Sterling";
    public const string SeededAdminLastName = "Diaz";
    public const string SeededAdminIdentificationNumber = "00118490614";
    public const string SeededAdminPhone = "8099892465";

    public static readonly Guid CategoryHogarId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid CategoryDomicilioId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid CategoryMedicosId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    // Fixed GUIDs for test nurses
    public static readonly Dictionary<string, Guid> NurseIds = new()
    {
        { "Lorea", Guid.Parse("f0000000-0000-0000-0000-000000000001") },
        { "Charleny", Guid.Parse("f0000000-0000-0000-0000-000000000002") },
        { "Valentin", Guid.Parse("f0000000-0000-0000-0000-000000000003") },
        { "Marel", Guid.Parse("f0000000-0000-0000-0000-000000000004") },
        { "Liliana", Guid.Parse("f0000000-0000-0000-0000-000000000005") },
        { "Clari", Guid.Parse("f0000000-0000-0000-0000-000000000006") },
        { "Solano", Guid.Parse("f0000000-0000-0000-0000-000000000007") },
        { "Angela Maria", Guid.Parse("f0000000-0000-0000-0000-000000000008") },
        { "Karen", Guid.Parse("f0000000-0000-0000-0000-000000000009") },
        { "Cristina", Guid.Parse("f0000000-0000-0000-0000-000000000010") },
        { "Figueredo", Guid.Parse("f0000000-0000-0000-0000-000000000011") },
        { "Annie", Guid.Parse("f0000000-0000-0000-0000-000000000012") },
        { "Zoila", Guid.Parse("f0000000-0000-0000-0000-000000000013") },
        { "Maria Isabel", Guid.Parse("f0000000-0000-0000-0000-000000000014") },
        { "Emilina", Guid.Parse("f0000000-0000-0000-0000-000000000015") },
        { "Cindy", Guid.Parse("f0000000-0000-0000-0000-000000000016") },
        { "Agustina", Guid.Parse("f0000000-0000-0000-0000-000000000017") },
        { "Johanna", Guid.Parse("f0000000-0000-0000-0000-000000000018") },
        { "Miranda", Guid.Parse("f0000000-0000-0000-0000-000000000019") },
        { "Miguelina", Guid.Parse("f0000000-0000-0000-0000-000000000020") },
        { "Celai", Guid.Parse("f0000000-0000-0000-0000-000000000021") },
        { "De Los Santos", Guid.Parse("f0000000-0000-0000-0000-000000000022") },
    };

    public static readonly Guid TestClientId = Guid.Parse("e0000000-0000-0000-0000-000000000001");

    public static async Task EnsureSeededAsync(NursingCareDbContext db, CancellationToken cancellationToken = default)
    {
        // Ensure system roles exist first (required by EnsureSeededAdminAsync)
        EnsureSystemRoles(db);

        await EnsureSeededAdminAsync(db, cancellationToken);

        var catalogsExist = await db.CareRequestCategoryCatalogs.AnyAsync(cancellationToken);
        if (!catalogsExist)
        {
            await SeedCatalogsAsync(db, cancellationToken);
        }

        // Always ensure test nurses exist - delete old ones and recreate
        var totalNurseCount = await db.Users
            .Where(x => x.ProfileType == UserProfileType.NURSE)
            .CountAsync(cancellationToken);

        if (totalNurseCount != NurseIds.Count)
        {
            await SeedUsersAndNursesAsync(db, cancellationToken);
        }
    }

    private static void EnsureSystemRoles(NursingCareDbContext db)
    {
        var existingRoleIds = db.Roles
            .Select(role => role.Id)
            .ToHashSet();

        var missingRoles = SystemRoles.Defaults
            .Where(role => !existingRoleIds.Contains(role.Id))
            .Select(role => new Role
            {
                Id = role.Id,
                Name = role.Name
            })
            .ToArray();

        if (missingRoles.Length == 0)
        {
            return;
        }

        db.Roles.AddRange(missingRoles);
        db.SaveChanges();
    }

    private static async Task EnsureSeededAdminAsync(NursingCareDbContext db, CancellationToken cancellationToken)
    {
        var adminRole = await db.Roles
            .SingleAsync(role => role.Name == SystemRoles.Admin, cancellationToken);

        var existingUser = await db.Users
            .Include(user => user.NurseProfile)
            .Include(user => user.ClientProfile)
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Email == SeededAdminEmail, cancellationToken);

        if (existingUser is not null && existingUser.ProfileType != UserProfileType.ADMIN)
        {
            db.Users.Remove(existingUser);
            await db.SaveChangesAsync(cancellationToken);
            existingUser = null;
        }

        if (existingUser is null)
        {
            var user = new User
            {
                Id = SeededAdminId,
                Name = SeededAdminName,
                LastName = SeededAdminLastName,
                IdentificationNumber = SeededAdminIdentificationNumber,
                Phone = SeededAdminPhone,
                Email = SeededAdminEmail,
                DisplayName = $"{SeededAdminName} {SeededAdminLastName}",
                ProfileType = UserProfileType.ADMIN,
                PasswordHash = HashPassword(SeededAdminPassword),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                FailedLoginAttemptCount = 0,
                ResetPasswordFailedAttemptCount = 0
            };

            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = adminRole.Id,
                Role = adminRole
            });

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        existingUser.Name = SeededAdminName;
        existingUser.LastName = SeededAdminLastName;
        existingUser.IdentificationNumber = SeededAdminIdentificationNumber;
        existingUser.Phone = SeededAdminPhone;
        existingUser.DisplayName = $"{SeededAdminName} {SeededAdminLastName}";
        existingUser.PasswordHash = HashPassword(SeededAdminPassword);
        existingUser.IsActive = true;
        existingUser.LockedOutUntilUtc = null;
        existingUser.FailedLoginAttemptCount = 0;
        existingUser.FailedLoginWindowStartedAtUtc = null;

        if (existingUser.NurseProfile is not null)
        {
            db.Nurses.Remove(existingUser.NurseProfile);
        }

        if (existingUser.ClientProfile is not null)
        {
            db.Clients.Remove(existingUser.ClientProfile);
        }

        if (!existingUser.UserRoles.Any(userRole => userRole.RoleId == adminRole.Id))
        {
            existingUser.UserRoles.Add(new UserRole
            {
                UserId = existingUser.Id,
                RoleId = adminRole.Id,
                Role = adminRole
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCatalogsAsync(NursingCareDbContext db, CancellationToken cancellationToken = default)
    {

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

    private static async Task SeedUsersAndNursesAsync(NursingCareDbContext db, CancellationToken cancellationToken = default)
    {
        // Delete all existing test nurses and clients to ensure clean seeding
        var allNurses = await db.Users.Where(u => u.ProfileType == UserProfileType.NURSE).ToListAsync(cancellationToken);
        var allClients = await db.Users.Where(u => u.ProfileType == UserProfileType.CLIENT).ToListAsync(cancellationToken);

        if (allNurses.Any())
        {
            db.Users.RemoveRange(allNurses);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (allClients.Any())
        {
            db.Users.RemoveRange(allClients);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Create test client user
        var clientUser = new User
        {
            Id = TestClientId,
            Email = "client@test.com",
            ProfileType = UserProfileType.CLIENT,
            Name = "Test",
            LastName = "Client",
            DisplayName = "Test Client",
            PasswordHash = HashPassword("12345678"),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(clientUser);
        db.Clients.Add(new Client { UserId = TestClientId });

        // Create nurse users
        var nurseCategoryIds = new Dictionary<string, string>
        {
            { "Junior", "80000000-0000-0000-0000-000000000001" },
            { "Semisenior", "80000000-0000-0000-0000-000000000002" },
            { "Senior", "80000000-0000-0000-0000-000000000003" },
            { "Lider", "80000000-0000-0000-0000-000000000004" }
        };

        var nurseSpecialties = new[] { "Cuidado de adultos", "Cuidado pediatrico", "Cuidado geriatrico" };
        var categoryRotation = new[] { "Junior", "Semisenior", "Senior", "Lider" };
        var specialtyRotation = nurseSpecialties;

        int nurseIndex = 0;
        foreach (var (nurseName, nurseId) in NurseIds)
        {
            var nurseUser = new User
            {
                Id = nurseId,
                Email = $"{nurseName.ToLower().Replace(" ", ".")}@nurses.test",
                ProfileType = UserProfileType.NURSE,
                Name = nurseName.Split(' ')[0],
                LastName = nurseName.Contains(' ') ? string.Join(" ", nurseName.Split(' ').Skip(1)) : "Nurse",
                DisplayName = nurseName,
                Phone = $"809555{(nurseIndex + 1000):D4}",
                IdentificationNumber = $"402000{(nurseIndex + 10000):D5}",
                PasswordHash = HashPassword("12345678"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Users.Add(nurseUser);

            var category = categoryRotation[nurseIndex % categoryRotation.Length];
            var specialty = specialtyRotation[nurseIndex % specialtyRotation.Length];

            var nurseProfile = new Nurse
            {
                UserId = nurseId,
                IsActive = true,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
                Specialty = specialty,
                LicenseId = $"{nurseIndex + 100000}",
                BankName = "Bank Test",
                AccountNumber = $"{nurseIndex + 100000000}",
                Category = category
            };

            db.Nurses.Add(nurseProfile);
            nurseIndex++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string HashPassword(string password)
    {
        const int saltSize = 16;
        const int hashSize = 32;
        const int iterations = 10000;
        var algorithm = HashAlgorithmName.SHA256;

        byte[] salt = new byte[saltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            algorithm,
            hashSize
        );

        byte[] hashWithSalt = new byte[saltSize + hashSize];
        Array.Copy(salt, 0, hashWithSalt, 0, saltSize);
        Array.Copy(hash, 0, hashWithSalt, saltSize, hashSize);

        return Convert.ToBase64String(hashWithSalt);
    }
}

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.Admin;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Infrastructure.Persistence;

/// <summary>
/// Seeds full lifecycle data across all care request statuses, additional payroll periods,
/// audit log entries, admin notifications, deduction records, and payroll line overrides.
/// Context: Dominican Republic private nursing care business.
/// Idempotent: exits early when care request count already exceeds the seed threshold.
/// </summary>
public static class FullLifecycleSeeding
{
    // Fixed GUIDs for the 5 additional client users added by this seeder.
    public static readonly Guid ClientCarmenId   = Guid.Parse("e0000000-0000-0000-0000-000000000011");
    public static readonly Guid ClientAnaId      = Guid.Parse("e0000000-0000-0000-0000-000000000012");
    public static readonly Guid ClientRosaId     = Guid.Parse("e0000000-0000-0000-0000-000000000013");
    public static readonly Guid ClientLuisaId    = Guid.Parse("e0000000-0000-0000-0000-000000000014");
    public static readonly Guid ClientBeatrizId  = Guid.Parse("e0000000-0000-0000-0000-000000000015");

    // Fixed GUIDs for 3 pending (IsActive=false) nurse profiles added by this seeder.
    public static readonly Guid PendingNurse1Id  = Guid.Parse("f0000000-0000-0000-0000-000000000023");
    public static readonly Guid PendingNurse2Id  = Guid.Parse("f0000000-0000-0000-0000-000000000024");
    public static readonly Guid PendingNurse3Id  = Guid.Parse("f0000000-0000-0000-0000-000000000025");

    public static async Task SeedWithContextAsync(NursingCareDbContext db, CancellationToken cancellationToken = default)
    {
        // Idempotency guard: CareRequestSeeding inserts 22. If > 30 already exist, we already ran.
        if (await db.CareRequests.CountAsync(cancellationToken) > 30)
        {
            return;
        }

        // ── 1. Additional client users ────────────────────────────────────────────────
        await EnsureAdditionalClientsAsync(db, cancellationToken);

        // ── 2. Care requests across all statuses ──────────────────────────────────────
        var careRequests = BuildCareRequests();
        db.CareRequests.AddRange(careRequests);
        await db.SaveChangesAsync(cancellationToken);

        // ── 3. Additional payroll periods (March Closed + July future Open) ──────────
        var (marchPeriod, _) = await EnsureAdditionalPayrollPeriodsAsync(db, cancellationToken);

        // ── 4. Payroll lines for the closed March 2026 period ─────────────────────────
        await SeedMarchPayrollLinesAsync(db, marchPeriod, careRequests, cancellationToken);

        // ── 5. Audit log entries ───────────────────────────────────────────────────────
        await SeedAuditLogsAsync(db, cancellationToken);

        // ── 6. Admin notifications ────────────────────────────────────────────────────
        await SeedAdminNotificationsAsync(db, careRequests, marchPeriod, cancellationToken);

        // ── 7. Deduction records ──────────────────────────────────────────────────────
        await SeedDeductionRecordsAsync(db, marchPeriod, cancellationToken);

        // ── 8. Payroll line overrides ─────────────────────────────────────────────────
        await SeedPayrollLineOverridesAsync(db, marchPeriod, cancellationToken);

        // ── 9. PaymentReported status care requests ───────────────────────────────────
        await SeedPaymentReportedRequestsAsync(db, careRequests, cancellationToken);

        // ── 10. Pending nurse profiles (IsActive=false) ───────────────────────────────
        await SeedPendingNurseProfilesAsync(db, cancellationToken);

        // ── 11. Additional scheduled deduction lifecycle states ───────────────────────
        await SeedScheduledDeductionLifecycleStatesAsync(db, cancellationToken);

        // ── 12. Future scheduled care requests ────────────────────────────────────────
        await SeedFutureScheduledRequestsAsync(db, cancellationToken);

        Console.WriteLine("FullLifecycleSeeding: completed successfully.");
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 1. Additional clients
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task EnsureAdditionalClientsAsync(NursingCareDbContext db, CancellationToken cancellationToken)
    {
        var additionalClientIds = new[]
        {
            ClientCarmenId, ClientAnaId, ClientRosaId, ClientLuisaId, ClientBeatrizId,
        };

        var existingIds = await db.Users
            .Where(u => additionalClientIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToHashSetAsync(cancellationToken);

        var clientRole = await db.Roles.SingleAsync(r => r.Name == SystemRoles.Client, cancellationToken);

        // Name and LastName must match the check constraint: letters, spaces, accented chars only.
        // IdentificationNumber must be exactly 11 digits.
        // Phone must be exactly 10 digits.
        var clientData = new[]
        {
            (Id: ClientCarmenId,   Name: "Carmen",  LastName: "Lopez Rodriguez",  Cedula: "40212345678", Phone: "8095551234", Email: "carmen.lopez@ejemplo.com"),
            (Id: ClientAnaId,      Name: "Ana",     LastName: "Reyes Martinez",   Cedula: "40298765432", Phone: "8295554321", Email: "ana.reyes@ejemplo.com"),
            (Id: ClientRosaId,     Name: "Rosa",    LastName: "Mendez Familia",   Cedula: "40187654321", Phone: "8495553456", Email: "rosa.mendez@ejemplo.com"),
            (Id: ClientLuisaId,    Name: "Luisa",   LastName: "Santana Torres",   Cedula: "40176543210", Phone: "8095557890", Email: "luisa.santana@ejemplo.com"),
            (Id: ClientBeatrizId,  Name: "Beatriz", LastName: "Garcia Pena",      Cedula: "40165432109", Phone: "8295556789", Email: "beatriz.garcia@ejemplo.com"),
        };

        foreach (var cd in clientData)
        {
            if (existingIds.Contains(cd.Id))
            {
                continue;
            }

            var user = new User
            {
                Id = cd.Id,
                Email = cd.Email,
                ProfileType = UserProfileType.CLIENT,
                Name = cd.Name,
                LastName = cd.LastName,
                DisplayName = $"{cd.Name} {cd.LastName}",
                IdentificationNumber = cd.Cedula,
                Phone = cd.Phone,
                PasswordHash = HashPassword("12345678"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                FailedLoginAttemptCount = 0,
                ResetPasswordFailedAttemptCount = 0,
            };

            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = clientRole.Id,
                Role = clientRole,
            });

            db.Users.Add(user);
            db.Clients.Add(new Client { UserId = cd.Id });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 2. Care requests across all statuses
    // ─────────────────────────────────────────────────────────────────────────────────

    private static CareRequest[] BuildCareRequests()
    {
        var march10 = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        var march15 = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc);
        var march20 = new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc);
        var march25 = new DateTime(2026, 3, 25, 8, 0, 0, DateTimeKind.Utc);
        var april5  = new DateTime(2026, 4, 5,  8, 0, 0, DateTimeKind.Utc);
        var april10 = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);

        // All 22 nurses from the roster — distributed across lifecycle statuses.
        var n1  = CatalogSeeding.NurseIds["Lorea"];
        var n2  = CatalogSeeding.NurseIds["Charleny"];
        var n3  = CatalogSeeding.NurseIds["Valentin"];
        var n4  = CatalogSeeding.NurseIds["Marel"];
        var n5  = CatalogSeeding.NurseIds["Liliana"];
        var n6  = CatalogSeeding.NurseIds["Clari"];
        var n7  = CatalogSeeding.NurseIds["Solano"];
        var n8  = CatalogSeeding.NurseIds["Angela Maria"];
        var n9  = CatalogSeeding.NurseIds["Karen"];
        var n10 = CatalogSeeding.NurseIds["Cristina"];
        var n11 = CatalogSeeding.NurseIds["Figueredo"];
        var n12 = CatalogSeeding.NurseIds["Annie"];
        var n13 = CatalogSeeding.NurseIds["Zoila"];
        var n14 = CatalogSeeding.NurseIds["Maria Isabel"];
        var n15 = CatalogSeeding.NurseIds["Emilina"];
        var n16 = CatalogSeeding.NurseIds["Cindy"];
        var n17 = CatalogSeeding.NurseIds["Agustina"];
        var n18 = CatalogSeeding.NurseIds["Johanna"];
        var n19 = CatalogSeeding.NurseIds["Miranda"];
        var n20 = CatalogSeeding.NurseIds["Miguelina"];
        var n21 = CatalogSeeding.NurseIds["Celai"];
        var n22 = CatalogSeeding.NurseIds["De Los Santos"];

        var testClient = CatalogSeeding.TestClientId;

        var list = new List<CareRequest>();

        // ── Pending (5) ───────────────────────────────────────────────────────────────
        list.Add(Make(testClient,      null, "Cuidado post-operatorio a domicilio",      "hogar_diario",       "dia_completo", 5, "local",   "estandar", 12500m, april10));
        list.Add(Make(ClientCarmenId,  null, "Terapia respiratoria nocturna",             "domicilio_noche_12h","medio_dia",   3, "cercana", "moderada",  8250m, april10.AddHours(2)));
        list.Add(Make(ClientAnaId,     null, "Cuidado de adulto mayor en hogar",          "hogar_diario",       "dia_completo", 6, "local",   "estandar", 15000m, april10.AddHours(4)));
        list.Add(Make(ClientRosaId,    null, "Administración de medicamentos IV",          "medicamentos",       "sesion",       2, "local",   "estandar",  4000m, april10.AddHours(6)));
        list.Add(Make(ClientLuisaId,   null, "Curacion de heridas post-cirugia",           "curas",              "sesion",       3, "cercana", "moderada",  6600m, april10.AddHours(8)));

        // ── Approved (5) — nurses n13-n17 ─────────────────────────────────────────────
        list.Add(MakeApproved(testClient,      n13, "Cuidado intensivo domiciliario 24h",      "domicilio_24h",      "dia_completo", 7, "media",  "alta",     29400m, april5));
        list.Add(MakeApproved(ClientBeatrizId, n14, "Enfermeria general diaria",                "hogar_diario",       "dia_completo", 5, "local",  "estandar", 12500m, april5.AddHours(2)));
        list.Add(MakeApproved(ClientCarmenId,  n15, "Cuidado nocturno 12 horas",                "domicilio_noche_12h","medio_dia",    4, "cercana","moderada", 11000m, april5.AddHours(4)));
        list.Add(MakeApproved(ClientAnaId,     n16, "Sonda nasogastrica y cuidados",            "sonda_nasogastrica", "sesion",       2, "local",  "alta",      7200m, april5.AddHours(6)));
        list.Add(MakeApproved(ClientRosaId,    n17, "Hogar basico primer mes",                  "hogar_basico",       "mes",          1, "local",  "estandar", 55000m, april5.AddHours(8)));

        // ── Rejected (3) ──────────────────────────────────────────────────────────────
        list.Add(MakeRejected(testClient,
            "Servicio de suero intravenoso a domicilio",       "suero",        "sesion",       1, "media",  "estandar",  2400m, march25,
            "No hay enfermeras disponibles en la zona indicada para la fecha solicitada."));
        list.Add(MakeRejected(ClientCarmenId,
            "Cuidado de paciente con alta complejidad",         "hogar_diario", "dia_completo", 5, "lejana", "critica",  16900m, march25.AddHours(3),
            "La distancia supera el radio de cobertura activo. Contáctenos para reagendar."));
        list.Add(MakeRejected(ClientBeatrizId,
            "Colocación de sonda vesical urgente",              "sonda_vesical","sesion",       1, "local",  "estandar",  2000m, march25.AddHours(6),
            "Documentación médica incompleta. Adjunte orden médica firmada."));

        // ── Completed (5) — nurses n18-n22 ───────────────────────────────────────────
        list.Add(MakeCompleted(testClient,      n18, "Cuidado post-parto en hogar",              "hogar_diario", "dia_completo", 6, "local",  "estandar", 15000m, march15));
        list.Add(MakeCompleted(ClientAnaId,     n19, "Cuidado de adulto mayor con demencia",     "domicilio_24h","dia_completo", 5, "cercana","moderada", 19250m, march15.AddHours(2)));
        list.Add(MakeCompleted(ClientRosaId,    n20, "Inyecciones y curaciones diarias",         "curas",        "sesion",       4, "local",  "estandar",  8000m, march15.AddHours(4)));
        list.Add(MakeCompleted(ClientLuisaId,   n21, "Rehabilitacion post-operatoria en hogar",  "hogar_diario", "dia_completo", 5, "media",  "alta",     15000m, march15.AddHours(6)));
        list.Add(MakeCompleted(ClientBeatrizId, n22, "Terapia intravenosa ambulatoria",          "suero",        "sesion",       3, "cercana","moderada",  8250m, march15.AddHours(8)));

        // ── Cancelled (3) ─────────────────────────────────────────────────────────────
        list.Add(MakeCancelled(testClient,      "Cuidado preventivo adulto mayor",       "hogar_diario",      "dia_completo", 5, "local","estandar", 12500m, march20));
        list.Add(MakeCancelled(ClientCarmenId,  "Servicio nocturno domiciliario",        "domicilio_noche_12h","medio_dia",   3, "local","estandar",  7500m, march20.AddHours(3)));
        list.Add(MakeCancelled(ClientLuisaId,   "Control de sonda PEG en domicilio",    "sonda_peg",         "sesion",       1, "cercana","estandar", 4400m, march20.AddHours(6)));

        // ── Invoiced (5) — nurses n1-n5 ───────────────────────────────────────────────
        list.Add(MakeInvoiced(testClient,      n1,  "Cuidado intensivo dia completo",            "domicilio_24h","dia_completo", 7, "local", "alta",      24500m, march10,            "SOL-202603-0031"));
        list.Add(MakeInvoiced(ClientAnaId,     n2,  "Cuidado basico mensual hogar",              "hogar_basico", "mes",          1, "local", "estandar",  55000m, march10.AddHours(2), "SOL-202603-0032"));
        list.Add(MakeInvoiced(ClientRosaId,    n3,  "Administración de medicamentos inyectables","medicamentos", "sesion",       3, "cercana","moderada",  7260m, march10.AddHours(4), "SOL-202603-0033"));
        list.Add(MakeInvoiced(ClientBeatrizId, n4,  "Cuidado diario estandar hogar",             "hogar_diario", "dia_completo", 5, "local", "estandar",  12500m, march10.AddHours(6), "SOL-202603-0034"));
        list.Add(MakeInvoiced(ClientLuisaId,   n5,  "Terapia nocturna domiciliaria",             "domicilio_noche_12h","medio_dia",4,"media","estandar", 12000m, march10.AddHours(8), "SOL-202603-0035"));

        // ── Paid (4) — nurses n6-n9 ───────────────────────────────────────────────────
        list.Add(MakePaid(testClient,      n6,  "Servicio diario adulto mayor Marzo",  "hogar_diario",     "dia_completo", 5, "local",  "estandar", 12500m, march10,            "SOL-202603-0021", "TRF-BHD-00201"));
        list.Add(MakePaid(ClientCarmenId,  n7,  "Cuidado 24h paciente oncologico",     "domicilio_24h",    "dia_completo", 6, "media",  "alta",     25200m, march10.AddHours(2), "SOL-202603-0022", "TRF-BHD-00202"));
        list.Add(MakePaid(ClientAnaId,     n8,  "Enfermeria domiciliaria dia Marzo",   "domicilio_dia_12h","medio_dia",    5, "cercana","moderada", 15125m, march10.AddHours(4), "SOL-202603-0023", "TRF-BHD-00203"));
        list.Add(MakePaid(ClientRosaId,    n9,  "Curaciones post-cirugia Marzo",       "curas",            "sesion",       4, "local",  "estandar",  8000m, march10.AddHours(6), "SOL-202603-0024", "TRF-BHD-00204"));

        // ── Voided (2) — nurses n10-n11 ───────────────────────────────────────────────
        // Void is allowed from Completed or Invoiced status.
        list.Add(MakeVoidedFromCompleted(testClient,     n10, "Servicio cancelado por hospitalizacion",
            "hogar_diario","dia_completo", 3, "local","estandar", 7500m, march10,
            "Paciente ingresado a hospital de emergencia. Servicio no ejecutado."));
        list.Add(MakeVoidedFromCompleted(ClientBeatrizId,n11, "Suero anulado por cambio de tratamiento",
            "suero","sesion", 2, "local","estandar", 4000m, march10.AddHours(3),
            "Médico tratante cambió protocolo. Solicitud anulada de común acuerdo con cliente."));

        return list.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // Status-specific factory helpers
    // ─────────────────────────────────────────────────────────────────────────────────

    private static CareRequest Make(
        Guid clientId, Guid? nurseId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt)
    {
        var (basePrice, category, distMult, compMult, catFactor) = ResolvePricing(typeCode, distance, complexity);

        return CareRequest.Create(new CareRequestCreateParams
        {
            UserID = clientId,
            Description = description,
            CareRequestReason = description,
            CareRequestType = typeCode,
            UnitType = unitType,
            SuggestedNurse = null,
            AssignedNurse = nurseId,
            Unit = units,
            Price = basePrice,
            Total = total,
            ClientBasePrice = null,
            DistanceFactor = distance,
            ComplexityLevel = complexity,
            MedicalSuppliesCost = null,
            CareRequestDate = DateOnly.FromDateTime(createdAt),
            PricingCategoryCode = category,
            CategoryFactorSnapshot = catFactor,
            DistanceFactorMultiplierSnapshot = distMult,
            ComplexityMultiplierSnapshot = compMult,
            VolumeDiscountPercentSnapshot = 0,
            LineBeforeVolumeDiscount = null,
            UnitPriceAfterVolumeDiscount = null,
            SubtotalBeforeSupplies = null,
            CreatedAtUtc = createdAt,
        });
    }

    private static CareRequest MakeApproved(
        Guid clientId, Guid nurseId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt)
    {
        var cr = Make(clientId, nurseId, description, typeCode, unitType, units, distance, complexity, total, createdAt);
        cr.Approve(createdAt.AddHours(1));
        return cr;
    }

    private static CareRequest MakeRejected(
        Guid clientId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt, string reason)
    {
        var cr = Make(clientId, null, description, typeCode, unitType, units, distance, complexity, total, createdAt);
        cr.Reject(createdAt.AddHours(2), reason);
        return cr;
    }

    private static CareRequest MakeCancelled(
        Guid clientId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt)
    {
        var cr = Make(clientId, null, description, typeCode, unitType, units, distance, complexity, total, createdAt);
        cr.Cancel(createdAt.AddHours(3));
        return cr;
    }

    private static CareRequest MakeCompleted(
        Guid clientId, Guid nurseId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt)
    {
        var cr = Make(clientId, nurseId, description, typeCode, unitType, units, distance, complexity, total, createdAt);
        cr.Approve(createdAt.AddHours(1));
        cr.Complete(createdAt.AddHours(14), nurseId);
        return cr;
    }

    private static CareRequest MakeInvoiced(
        Guid clientId, Guid nurseId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt, string invoiceNumber)
    {
        var cr = MakeCompleted(clientId, nurseId, description, typeCode, unitType, units, distance, complexity, total, createdAt);
        cr.Invoice(invoiceNumber, createdAt.AddDays(1));
        return cr;
    }

    private static CareRequest MakePaid(
        Guid clientId, Guid nurseId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt, string invoiceNumber, string bankRef)
    {
        var cr = MakeInvoiced(clientId, nurseId, description, typeCode, unitType, units, distance, complexity, total, createdAt, invoiceNumber);
        cr.Pay(bankRef, createdAt.AddDays(2));
        return cr;
    }

    private static CareRequest MakeVoidedFromCompleted(
        Guid clientId, Guid nurseId, string description,
        string typeCode, string unitType, int units,
        string distance, string complexity, decimal total, DateTime createdAt, string voidReason)
    {
        var cr = MakeCompleted(clientId, nurseId, description, typeCode, unitType, units, distance, complexity, total, createdAt);
        cr.Void(voidReason, createdAt.AddDays(1));
        return cr;
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // Pricing lookup helper
    // ─────────────────────────────────────────────────────────────────────────────────

    private static (decimal BasePrice, string Category, decimal DistMult, decimal CompMult, decimal CatFactor) ResolvePricing(
        string typeCode, string distance, string complexity)
    {
        var basePrices = new Dictionary<string, decimal>
        {
            { "hogar_diario", 2500m },     { "hogar_basico", 55000m },    { "hogar_estandar", 60000m },
            { "hogar_premium", 65000m },   { "domicilio_dia_12h", 2500m }, { "domicilio_noche_12h", 2500m },
            { "domicilio_24h", 3500m },    { "suero", 2000m },             { "medicamentos", 2000m },
            { "sonda_vesical", 2000m },    { "sonda_nasogastrica", 3000m }, { "sonda_peg", 4000m },
            { "curas", 2000m },
        };

        var distMults = new Dictionary<string, decimal>
        {
            { "local", 1.0m }, { "cercana", 1.1m }, { "media", 1.2m }, { "lejana", 1.3m },
        };

        var compMults = new Dictionary<string, decimal>
        {
            { "estandar", 1.0m }, { "moderada", 1.1m }, { "alta", 1.2m }, { "critica", 1.3m },
        };

        var catFactors = new Dictionary<string, decimal>
        {
            { "hogar", 1.0m }, { "domicilio", 1.2m }, { "medicos", 1.5m },
        };

        string category = typeCode switch
        {
            var s when s.StartsWith("hogar")     => "hogar",
            var s when s.StartsWith("domicilio") => "domicilio",
            _                                    => "medicos",
        };

        return (basePrices[typeCode], category, distMults[distance], compMults[complexity], catFactors[category]);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 3. Additional payroll periods
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task<(PayrollPeriod March, PayrollPeriod Future)> EnsureAdditionalPayrollPeriodsAsync(
        NursingCareDbContext db, CancellationToken cancellationToken)
    {
        var marchStart  = new DateOnly(2026, 3, 1);
        var marchEnd    = new DateOnly(2026, 3, 31);

        // Future period: July 2026 — first quincena (1–15). Open status so it appears as
        // an upcoming period in the payroll calendar alongside the current-month Open period.
        var futureStart = new DateOnly(2026, 7, 1);
        var futureEnd   = new DateOnly(2026, 7, 15);

        var march = await db.PayrollPeriods
            .SingleOrDefaultAsync(p => p.StartDate == marchStart && p.EndDate == marchEnd, cancellationToken);

        if (march is null)
        {
            march = PayrollPeriod.Create(
                startDate: marchStart,
                endDate: marchEnd,
                cutoffDate: new DateOnly(2026, 3, 28),
                paymentDate: new DateOnly(2026, 4, 5),
                createdAtUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            march.Close(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
            db.PayrollPeriods.Add(march);
            await db.SaveChangesAsync(cancellationToken);
        }

        var future = await db.PayrollPeriods
            .SingleOrDefaultAsync(p => p.StartDate == futureStart && p.EndDate == futureEnd, cancellationToken);

        if (future is null)
        {
            future = PayrollPeriod.Create(
                startDate: futureStart,
                endDate: futureEnd,
                cutoffDate: new DateOnly(2026, 7, 13),
                paymentDate: new DateOnly(2026, 7, 18),
                createdAtUtc: DateTime.UtcNow);
            db.PayrollPeriods.Add(future);
            await db.SaveChangesAsync(cancellationToken);
        }

        return (march, future);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 4. Payroll lines for the closed March period
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedMarchPayrollLinesAsync(
        NursingCareDbContext db, PayrollPeriod marchPeriod, CareRequest[] careRequests, CancellationToken cancellationToken)
    {
        var rules = await db.CompensationRules.AsNoTracking().ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return;
        }

        var ruleLookup = rules.ToDictionary(r => r.CareRequestCategoryCode ?? "");

        var completedRequests = careRequests
            .Where(cr => cr.CompletedAtUtc.HasValue && cr.AssignedNurse.HasValue)
            .ToList();

        if (completedRequests.Count == 0)
        {
            return;
        }

        // The first two completed requests get a ServiceExecution + linked PayrollLine + adjustment.
        // All remaining completed requests get unlinked PayrollLines (serviceExecutionId: null).
        // This satisfies the unique constraint on ServiceExecution.CareRequestId while keeping
        // the adjustment data surfaced properly in both the execution and the payroll line.

        var adjustedRequests   = completedRequests.Take(2).ToList();
        var unadjustedRequests = completedRequests.Skip(2).ToList();

        var now = new DateTime(2026, 3, 31, 14, 0, 0, DateTimeKind.Utc);

        // ── Adjusted requests: ServiceExecution + linked PayrollLine + adjustment ─────
        foreach (var (cr, idx) in adjustedRequests.Select((c, i) => (c, i)))
        {
            var rule       = ruleLookup.GetValueOrDefault(cr.PricingCategoryCode ?? "") ?? rules.First();
            var executedAt = cr.CompletedAtUtc!.Value;
            var subtotal   = Math.Max(0m, cr.Total - (cr.MedicalSuppliesCost ?? 0m));

            var baseComp   = decimal.Round(subtotal * (rule.BaseCompensationPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var transport  = decimal.Round(subtotal * Math.Max(0m, (cr.DistanceFactorMultiplierSnapshot ?? 1m) - 1m) * (rule.TransportIncentivePercent / 100m), 2, MidpointRounding.AwayFromZero);
            var complexity = decimal.Round(subtotal * Math.Max(0m, (cr.ComplexityMultiplierSnapshot ?? 1m) - 1m) * (rule.ComplexityBonusPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var supplies   = decimal.Round((cr.MedicalSuppliesCost ?? 0m) * (rule.MedicalSuppliesPercent / 100m), 2, MidpointRounding.AwayFromZero);

            // Adjustment amounts: index 0 → +500 (Bono por turno extra), index 1 → -300 (Penalización por retraso).
            var (adjLabel, adjAmount, adjNotes, adjAt) = idx == 0
                ? ("Bono por turno extra",      500m,  "Enfermera cubrió turno adicional no registrado en sistema.", now)
                : ("Penalización por retraso",  -300m, "Retraso documentado superior a 30 minutos en inicio del turno.", now.AddHours(1));

            var exec = ServiceExecution.Create(
                careRequestId:                cr.Id,
                nurseUserId:                  cr.AssignedNurse!.Value,
                shiftRecordId:                null,
                compensationRuleId:           rule.Id,
                employmentType:               CompensationEmploymentType.PerService,
                variant:                      ServiceExecutionVariant.Standard,
                executedAtUtc:                executedAt,
                careRequestType:              cr.CareRequestType,
                unitType:                     cr.UnitType,
                unit:                         cr.Unit,
                pricingCategoryCode:          cr.PricingCategoryCode,
                distanceFactorCode:           cr.DistanceFactor,
                complexityLevelCode:          cr.ComplexityLevel,
                basePrice:                    cr.Price,
                careRequestTotal:             cr.Total,
                clientBasePrice:              cr.ClientBasePrice ?? cr.Price,
                categoryFactorSnapshot:       cr.CategoryFactorSnapshot ?? 1m,
                distanceMultiplierSnapshot:   cr.DistanceFactorMultiplierSnapshot ?? 1m,
                complexityMultiplierSnapshot: cr.ComplexityMultiplierSnapshot ?? 1m,
                volumeDiscountPercentSnapshot:cr.VolumeDiscountPercentSnapshot ?? 0,
                subtotalBeforeSupplies:       subtotal,
                medicalSuppliesCost:          cr.MedicalSuppliesCost ?? 0m,
                ruleBaseCompensationPercent:  rule.BaseCompensationPercent,
                ruleFixedAmountPerUnit:       rule.FixedAmountPerUnit,
                ruleTransportIncentivePercent:rule.TransportIncentivePercent,
                ruleComplexityBonusPercent:   rule.ComplexityBonusPercent,
                ruleMedicalSuppliesPercent:   rule.MedicalSuppliesPercent,
                ruleVariantPercent:           rule.BaseCompensationPercent,
                baseCompensation:             baseComp,
                transportIncentive:           transport,
                complexityBonus:              complexity,
                medicalSuppliesCompensation:  supplies,
                adjustmentsTotal:             0m,
                deductionsTotal:              0m,
                manualOverrideAmount:         null,
                notes:                        "Ejecucion seed — lifecycle con ajuste",
                createdAtUtc:                 executedAt);

            db.ServiceExecutions.Add(exec);
            await db.SaveChangesAsync(cancellationToken); // persist exec to get a stable Id

            // Apply the adjustment to the execution.
            exec.SetAdjustmentsTotal(adjAmount, adjAt);

            var adj = CompensationAdjustment.Create(
                serviceExecutionId: exec.Id,
                label:              adjLabel,
                amount:             adjAmount,
                notes:              adjNotes,
                createdAtUtc:       adjAt);

            db.CompensationAdjustments.Add(adj);

            // PayrollLine is linked to this execution and carries the same adjustment.
            var line = PayrollLine.Create(
                payrollPeriodId:            marchPeriod.Id,
                nurseUserId:                cr.AssignedNurse!.Value,
                serviceExecutionId:         exec.Id,
                description:                $"Servicio {cr.CareRequestType} — lifecycle seed (ajustado)",
                baseCompensation:           baseComp,
                transportIncentive:         transport,
                complexityBonus:            complexity,
                medicalSuppliesCompensation:supplies,
                adjustmentsTotal:           adjAmount,
                deductionsTotal:            0m,
                createdAtUtc:               executedAt);

            db.PayrollLines.Add(line);
            await db.SaveChangesAsync(cancellationToken);
        }

        // ── Remaining completed requests: unlinked PayrollLines ───────────────────────
        var unadjustedLines = new List<PayrollLine>();
        foreach (var cr in unadjustedRequests)
        {
            var rule       = ruleLookup.GetValueOrDefault(cr.PricingCategoryCode ?? "") ?? rules.First();
            var executedAt = cr.CompletedAtUtc!.Value;
            var subtotal   = Math.Max(0m, cr.Total - (cr.MedicalSuppliesCost ?? 0m));

            var baseComp   = decimal.Round(subtotal * (rule.BaseCompensationPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var transport  = decimal.Round(subtotal * Math.Max(0m, (cr.DistanceFactorMultiplierSnapshot ?? 1m) - 1m) * (rule.TransportIncentivePercent / 100m), 2, MidpointRounding.AwayFromZero);
            var complexity = decimal.Round(subtotal * Math.Max(0m, (cr.ComplexityMultiplierSnapshot ?? 1m) - 1m) * (rule.ComplexityBonusPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var supplies   = decimal.Round((cr.MedicalSuppliesCost ?? 0m) * (rule.MedicalSuppliesPercent / 100m), 2, MidpointRounding.AwayFromZero);

            unadjustedLines.Add(PayrollLine.Create(
                payrollPeriodId:            marchPeriod.Id,
                nurseUserId:                cr.AssignedNurse!.Value,
                serviceExecutionId:         null,
                description:                $"Servicio {cr.CareRequestType} — lifecycle seed",
                baseCompensation:           baseComp,
                transportIncentive:         transport,
                complexityBonus:            complexity,
                medicalSuppliesCompensation:supplies,
                adjustmentsTotal:           0m,
                deductionsTotal:            0m,
                createdAtUtc:               executedAt));
        }

        if (unadjustedLines.Count > 0)
        {
            db.PayrollLines.AddRange(unadjustedLines);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 5. Audit logs
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedAuditLogsAsync(NursingCareDbContext db, CancellationToken cancellationToken)
    {
        var adminId  = CatalogSeeding.SeededAdminId;
        var march10  = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
        var april1   = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var april2   = new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc);
        var april3   = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc);

        var logs = new List<AuditLog>
        {
            AuditEntry(adminId, "Admin", "ApproveRequest",    "CareRequest",    "lifecycle-approved-1",  "Solicitud aprobada y enfermera asignada.",                     march10),
            AuditEntry(adminId, "Admin", "ApproveRequest",    "CareRequest",    "lifecycle-approved-2",  "Solicitud aprobada.",                                          march10.AddMinutes(5)),
            AuditEntry(adminId, "Admin", "RejectRequest",     "CareRequest",    "lifecycle-rejected-1",  "Solicitud rechazada: zona fuera de cobertura.",                march10.AddMinutes(10)),
            AuditEntry(adminId, "Admin", "RejectRequest",     "CareRequest",    "lifecycle-rejected-2",  "Solicitud rechazada: documentación incompleta.",               march10.AddMinutes(15)),
            AuditEntry(adminId, "Admin", "CompleteRequest",   "CareRequest",    "lifecycle-completed-1", "Servicio marcado como completado.",                            march10.AddMinutes(20)),
            AuditEntry(adminId, "Admin", "CompleteRequest",   "CareRequest",    "lifecycle-completed-2", "Servicio completado por enfermera asignada.",                  march10.AddMinutes(25)),
            AuditEntry(adminId, "Admin", "InvoiceRequest",    "CareRequest",    "lifecycle-invoiced-1",  "Factura SOL-202603-0031 generada.",                            march10.AddMinutes(30)),
            AuditEntry(adminId, "Admin", "InvoiceRequest",    "CareRequest",    "lifecycle-invoiced-2",  "Factura SOL-202603-0032 generada.",                            march10.AddMinutes(35)),
            AuditEntry(adminId, "Admin", "RecordPayment",     "CareRequest",    "lifecycle-paid-1",      "Pago registrado: TRF-BHD-00201.",                              march10.AddMinutes(40)),
            AuditEntry(adminId, "Admin", "RecordPayment",     "CareRequest",    "lifecycle-paid-2",      "Pago registrado: TRF-BHD-00202.",                              march10.AddMinutes(45)),
            AuditEntry(adminId, "Admin", "VoidRequest",       "CareRequest",    "lifecycle-voided-1",    "Solicitud anulada: paciente hospitalizado.",                   march10.AddMinutes(50)),
            AuditEntry(adminId, "Admin", "VoidRequest",       "CareRequest",    "lifecycle-voided-2",    "Solicitud anulada: cambio de tratamiento médico.",             march10.AddMinutes(55)),
            AuditEntry(adminId, "Admin", "UserLogin",         "User",           adminId.ToString(),      "Inicio de sesión exitoso desde portal de administración.",     march10.AddHours(1)),
            AuditEntry(adminId, "Admin", "UserLogin",         "User",           adminId.ToString(),      "Inicio de sesión exitoso.",                                    march10.AddHours(2)),
            AuditEntry(adminId, "Admin", "UpdateSetting",     "SystemSetting",  "CARE_REQUEST_AGING_THRESHOLD_HOURS", "Umbral de envejecimiento actualizado de 48 a 72 horas.", march10.AddHours(3)),
            AuditEntry(adminId, "Admin", "UpdateSetting",     "SystemSetting",  "DASHBOARD_HIGH_SEVERITY_THRESHOLD",  "Umbral de severidad actualizado a 85.",              march10.AddHours(4)),
            AuditEntry(adminId, "Admin", "ClosePayrollPeriod","PayrollPeriod",  "march-2026",            "Período de nómina de marzo 2026 cerrado.",                     april1),
            AuditEntry(adminId, "Admin", "ApproveOverride",   "PayrollLineOverride","lifecycle-override-1","Override de línea de nómina aprobado.",                      april2),
            AuditEntry(adminId, "Admin", "NurseProfileReview","Nurse",          CatalogSeeding.NurseIds["Lorea"].ToString(), "Perfil de enfermera revisado y actualizado.", april3),
            AuditEntry(adminId, "Admin", "CancelRequest",     "CareRequest",    "lifecycle-cancelled-1", "Solicitud cancelada a pedido del cliente.",                    march10.AddMinutes(60)),
        };

        db.AuditLogs.AddRange(logs);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AuditLog AuditEntry(Guid actorId, string role, string action, string entityType, string entityId, string notes, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            ActorRole = role,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Notes = notes,
            CreatedAtUtc = createdAt,
        };

    // ─────────────────────────────────────────────────────────────────────────────────
    // 6. Admin notifications
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedAdminNotificationsAsync(
        NursingCareDbContext db, CareRequest[] careRequests, PayrollPeriod marchPeriod, CancellationToken cancellationToken)
    {
        var adminId = CatalogSeeding.SeededAdminId;
        var march10 = new DateTime(2026, 3, 10, 8, 30, 0, DateTimeKind.Utc);

        // Resolve the specific seeded request each notification refers to so "Abrir contexto"
        // deep-links to that request's detail (the most specific context where the action took
        // place) instead of the bare list. Production handlers already do this via
        // DeepLinkPath: /admin/care-requests/{id}; the seed data must match that contract.
        // Descriptions are unique within BuildCareRequests, so they are a stable lookup key.
        Guid? RequestId(string description) =>
            careRequests.FirstOrDefault(cr => cr.Description == description)?.Id;

        // Falls back to the list when the request can't be resolved (defensive — descriptions
        // above are expected to match), mirroring the bare path used before this change.
        string CareRequestLink(Guid? id) =>
            id is null ? "/admin/care-requests" : $"/admin/care-requests/{id}";

        var rejectedCarmenId = RequestId("Cuidado de paciente con alta complejidad");
        var completedPostPartoId = RequestId("Cuidado post-parto en hogar");
        var pendingAnaId = RequestId("Cuidado de adulto mayor en hogar");
        var paidMarchId = RequestId("Servicio diario adulto mayor Marzo");
        var invoicedIntensivoId = RequestId("Cuidado intensivo dia completo");

        var notifications = new List<AdminNotification>
        {
            // Unread — high severity
            Notification(adminId, "CareRequest", "High",
                "Solicitud rechazada requiere atención",
                "La solicitud de Carmen Lopez fue rechazada por falta de cobertura en zona lejana. Revise opciones de reasignación.",
                "CareRequest", rejectedCarmenId?.ToString(), CareRequestLink(rejectedCarmenId), requiresAction: true, readAt: null, createdAt: march10),

            Notification(adminId, "Nurse", "High",
                "Perfil de enfermera pendiente de revisión",
                "El perfil de la enfermera tiene documentos vencidos que requieren actualización antes del próximo servicio.",
                "Nurse", CatalogSeeding.NurseIds["Figueredo"].ToString(), $"/admin/nurse-profiles/{CatalogSeeding.NurseIds["Figueredo"]}", requiresAction: true, readAt: null, createdAt: march10.AddMinutes(10)),

            // Unread — medium severity
            Notification(adminId, "CareRequest", "Medium",
                "Servicio completado exitosamente",
                "El servicio de cuidado post-parto fue completado satisfactoriamente.",
                "CareRequest", completedPostPartoId?.ToString(), CareRequestLink(completedPostPartoId), requiresAction: false, readAt: null, createdAt: march10.AddMinutes(20)),

            Notification(adminId, "Payroll", "Medium",
                "Período de nómina pendiente de cierre",
                "El período de nómina de marzo 2026 está pendiente de revisión y cierre. Fecha límite: 1 de abril.",
                "PayrollPeriod", marchPeriod.Id.ToString(), $"/admin/payroll/periods?periodId={marchPeriod.Id}", requiresAction: true, readAt: null, createdAt: march10.AddMinutes(30)),

            Notification(adminId, "CareRequest", "Medium",
                "Solicitud próxima a vencer",
                "La solicitud de Ana Reyes lleva más de 48 horas en estado Pendiente sin asignación de enfermera.",
                "CareRequest", pendingAnaId?.ToString(), CareRequestLink(pendingAnaId), requiresAction: true, readAt: null, createdAt: march10.AddMinutes(40)),

            // Read — medium severity
            Notification(adminId, "CareRequest", "Medium",
                "Pago registrado correctamente",
                "El pago con referencia TRF-BHD-00201 fue registrado para la solicitud de servicio diario de marzo.",
                "CareRequest", paidMarchId?.ToString(), CareRequestLink(paidMarchId), requiresAction: false, readAt: march10.AddHours(1), createdAt: march10.AddMinutes(50)),

            Notification(adminId, "CareRequest", "Medium",
                "Factura generada",
                "La factura SOL-202603-0031 fue generada exitosamente para el servicio de cuidado intensivo.",
                "CareRequest", invoicedIntensivoId?.ToString(), CareRequestLink(invoicedIntensivoId), requiresAction: false, readAt: march10.AddHours(2), createdAt: march10.AddMinutes(60)),

            Notification(adminId, "Payroll", "Medium",
                "Nómina de marzo cerrada",
                "El período de nómina de marzo 2026 fue cerrado exitosamente con líneas de nómina procesadas.",
                "PayrollPeriod", marchPeriod.Id.ToString(), $"/admin/payroll/periods?periodId={marchPeriod.Id}", requiresAction: false, readAt: march10.AddHours(3), createdAt: new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Utc)),

            // Low severity
            Notification(adminId, "Settings", "Low",
                "Configuración actualizada",
                "El umbral de envejecimiento de solicitudes fue actualizado de 48 a 72 horas por el administrador.",
                null, null, "/settings", requiresAction: false, readAt: march10.AddHours(4), createdAt: march10.AddHours(3)),

            Notification(adminId, "Nurse", "Low",
                "Enfermera inactiva detectada",
                "La enfermera Emilina no ha ejecutado servicios en los últimos 30 días. Considere revisar su disponibilidad.",
                "Nurse", CatalogSeeding.NurseIds["Emilina"].ToString(), $"/admin/nurse-profiles/{CatalogSeeding.NurseIds["Emilina"]}", requiresAction: false, readAt: null, createdAt: march10.AddHours(5)),
        };

        db.AdminNotifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AdminNotification Notification(
        Guid recipientId, string category, string severity,
        string title, string body,
        string? entityType, string? entityId, string? deepLinkPath,
        bool requiresAction, DateTime? readAt, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientId,
            Category = category,
            Severity = severity,
            Title = title,
            Body = body,
            EntityType = entityType,
            EntityId = entityId,
            DeepLinkPath = deepLinkPath,
            RequiresAction = requiresAction,
            IsDismissed = false,
            CreatedBySystem = true,
            CreatedAtUtc = createdAt,
            ReadAtUtc = readAt,
        };

    // ─────────────────────────────────────────────────────────────────────────────────
    // 7. Deduction records
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedDeductionRecordsAsync(
        NursingCareDbContext db, PayrollPeriod marchPeriod, CancellationToken cancellationToken)
    {
        var march31 = new DateTime(2026, 3, 31, 12, 0, 0, DateTimeKind.Utc);

        var deductions = new[]
        {
            DeductionRecord.Create(
                nurseUserId: CatalogSeeding.NurseIds["Lorea"],
                payrollPeriodId: marchPeriod.Id,
                deductionType: DeductionType.Advance,
                label: "Adelanto de sueldo Marzo",
                amount: 3000m,
                notes: "Adelanto solicitado el 15 de marzo. Descontado en nomina de cierre.",
                effectiveAtUtc: march31,
                createdAtUtc: march31),

            DeductionRecord.Create(
                nurseUserId: CatalogSeeding.NurseIds["Valentin"],
                payrollPeriodId: marchPeriod.Id,
                deductionType: DeductionType.Other,
                label: "Descuento por uniformes Marzo",
                amount: 800m,
                notes: "Dos conjuntos de uniformes entregados. Deduccion por acuerdo previo.",
                effectiveAtUtc: march31,
                createdAtUtc: march31),

            DeductionRecord.Create(
                nurseUserId: CatalogSeeding.NurseIds["Marel"],
                payrollPeriodId: marchPeriod.Id,
                deductionType: DeductionType.Advance,
                label: "Adelanto por emergencia familiar Marzo",
                amount: 2500m,
                notes: "Adelanto de emergencia aprobado por gerencia el 22 de marzo.",
                effectiveAtUtc: march31,
                createdAtUtc: march31),

            DeductionRecord.Create(
                nurseUserId: CatalogSeeding.NurseIds["Liliana"],
                payrollPeriodId: marchPeriod.Id,
                deductionType: DeductionType.Loan,
                label: "Prestamo personal cuota 1 de 4",
                amount: 2000m,
                notes: "Prestamo nuevo aprobado en marzo 2026. Primera cuota.",
                effectiveAtUtc: march31,
                createdAtUtc: march31),
        };

        db.DeductionRecords.AddRange(deductions);
        await db.SaveChangesAsync(cancellationToken);

        await SeedScheduledDeductionsAsync(db, marchPeriod, march31, cancellationToken);
    }

    private static async Task SeedScheduledDeductionsAsync(
        NursingCareDbContext db, PayrollPeriod marchPeriod, DateTime march31, CancellationToken cancellationToken)
    {
        var adminId = CatalogSeeding.SeededAdminId;
        var janStart = new DateOnly(2026, 1, 16);

        // Charleny: amortizing loan (9,000 over 6 monthly installments of 1,500). Its 2nd cuota
        // lands in the March period; one prior cuota is already settled.
        var loan = ScheduledDeduction.CreateAmortizing(
            nurseUserId: CatalogSeeding.NurseIds["Charleny"],
            deductionType: DeductionType.Loan,
            label: "Préstamo personal",
            principalAmount: 9000m,
            interestRatePercent: 0m,
            totalInstallments: 6,
            cadence: DeductionCadence.Monthly,
            startPeriodDate: janStart,
            notes: "Préstamo aprobado en enero 2026, descontado en 6 cuotas mensuales.",
            createdByUserId: adminId,
            createdAtUtc: march31);
        loan.SyncGeneratedCount(2);
        loan.ApplySettlement(1, 1500m, march31); // first cuota already paid in a prior period

        // Lorea: open-ended recurring health insurance (500 each month).
        var insurance = ScheduledDeduction.CreateRecurring(
            nurseUserId: CatalogSeeding.NurseIds["Lorea"],
            deductionType: DeductionType.Insurance,
            label: "Seguro Médico",
            recurringAmount: 500m,
            cadence: DeductionCadence.Monthly,
            startPeriodDate: janStart,
            endDate: null,
            maxOccurrences: null,
            notes: "Plan de seguro médico mensual.",
            createdByUserId: adminId,
            createdAtUtc: march31);
        insurance.SyncGeneratedCount(3);

        db.ScheduledDeductions.AddRange(loan, insurance);

        db.DeductionRecords.AddRange(
            DeductionRecord.Create(
                nurseUserId: CatalogSeeding.NurseIds["Charleny"],
                payrollPeriodId: marchPeriod.Id,
                deductionType: DeductionType.Loan,
                label: "Préstamo personal · cuota 2 de 6",
                amount: 1500m,
                notes: null,
                effectiveAtUtc: march31,
                createdAtUtc: march31,
                scheduledDeductionId: loan.Id,
                installmentSequence: 2),
            DeductionRecord.Create(
                nurseUserId: CatalogSeeding.NurseIds["Lorea"],
                payrollPeriodId: marchPeriod.Id,
                deductionType: DeductionType.Insurance,
                label: "Seguro Médico",
                amount: 500m,
                notes: null,
                effectiveAtUtc: march31,
                createdAtUtc: march31,
                scheduledDeductionId: insurance.Id,
                installmentSequence: 3));

        await db.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 8. Payroll line overrides
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedPayrollLineOverridesAsync(
        NursingCareDbContext db, PayrollPeriod marchPeriod, CancellationToken cancellationToken)
    {
        var adminId      = CatalogSeeding.SeededAdminId;
        // Use a second admin GUID to satisfy the "cannot self-approve" business rule.
        var secondAdminId = Guid.Parse("d0000000-0000-0000-0000-000000000002");

        var marchLines = await db.PayrollLines
            .Where(pl => pl.PayrollPeriodId == marchPeriod.Id)
            .OrderBy(pl => pl.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (marchLines.Count < 3)
        {
            return;
        }

        var requestedAt1 = new DateTime(2026, 4, 2, 9,  0, 0, DateTimeKind.Utc);
        var requestedAt2 = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc);
        var requestedAt3 = new DateTime(2026, 4, 3, 9,  0, 0, DateTimeKind.Utc);

        // Override 1 — Approved
        var override1 = PayrollLineOverride.Create(
            payrollLineId: marchLines[0].Id,
            requestedByUserId: secondAdminId,
            overrideAmount: marchLines[0].NetCompensation + 500m,
            reason: "Ajuste manual: enfermera cubrio turno adicional no registrado en sistema.",
            requestedAtUtc: requestedAt1);
        override1.Approve(adminId, requestedAt1.AddHours(2));

        // Override 2 — Approved
        var override2 = PayrollLineOverride.Create(
            payrollLineId: marchLines[1].Id,
            requestedByUserId: secondAdminId,
            overrideAmount: marchLines[1].NetCompensation + 800m,
            reason: "Correccion: servicio de mayor complejidad registrado incorrectamente como estandar.",
            requestedAtUtc: requestedAt2);
        override2.Approve(adminId, requestedAt2.AddHours(1));

        // Override 3 — Pending
        var override3 = PayrollLineOverride.Create(
            payrollLineId: marchLines[2].Id,
            requestedByUserId: secondAdminId,
            overrideAmount: marchLines[2].NetCompensation - 200m,
            reason: "Correccion de deduccion por ausencia justificada pendiente de validacion.",
            requestedAtUtc: requestedAt3);

        db.PayrollLineOverrides.AddRange(override1, override2, override3);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 9. PaymentReported care requests
    // ─────────────────────────────────────────────────────────────────────────────────

    // Minimal valid 1×1 PNG (67 bytes): PNG signature + IHDR + IDAT (1 white pixel) + IEND.
    private static readonly byte[] MinimalPngBytes = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR length + type
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // width=1, height=1
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // bit depth=8, colortype=2 (RGB), CRC
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT length + type
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0x3F, // IDAT data (deflate compressed white pixel)
        0x00, 0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59, // continuation
        0xE7, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // CRC + IEND type
        0x44, 0xAE, 0x42, 0x60, 0x82,                   // IEND CRC
    };

    private static async Task SeedPaymentReportedRequestsAsync(
        NursingCareDbContext db, CareRequest[] existingCareRequests, CancellationToken cancellationToken)
    {
        var adminId = CatalogSeeding.SeededAdminId;
        var march8  = new DateTime(2026, 3, 8, 8, 0, 0, DateTimeKind.Utc);
        var march9  = new DateTime(2026, 3, 9, 8, 0, 0, DateTimeKind.Utc);

        var n5 = CatalogSeeding.NurseIds["Liliana"];
        var n6 = CatalogSeeding.NurseIds["Clari"];

        // Build and save two Invoiced care requests first (paymentproof needs the saved ID).
        var pr1 = MakeInvoiced(ClientLuisaId,  n5, "Atención domiciliaria nocturna reportada",
            "domicilio_noche_12h", "medio_dia", 3, "cercana", "moderada", 8250m, march8,  "SOL-202603-0036");
        var pr2 = MakeInvoiced(ClientBeatrizId, n6, "Cuidado diario con pago reportado",
            "hogar_diario",        "dia_completo", 4, "local", "estandar", 10000m, march9, "SOL-202603-0037");

        db.CareRequests.AddRange(pr1, pr2);
        await db.SaveChangesAsync(cancellationToken);

        // Create PaymentProofs (require saved care request IDs).
        var proof1 = PaymentProof.Create(
            careRequestId:    pr1.Id,
            content:          MinimalPngBytes,
            contentType:      "image/png",
            note:             "Transferencia BHD ref. TRF-REPORT-001",
            uploadedByUserId: adminId,
            uploadedAtUtc:    march8.AddDays(2));

        var proof2 = PaymentProof.Create(
            careRequestId:    pr2.Id,
            content:          MinimalPngBytes,
            contentType:      "image/png",
            note:             "Comprobante de pago ref. TRF-REPORT-002",
            uploadedByUserId: adminId,
            uploadedAtUtc:    march9.AddDays(2));

        db.PaymentProofs.AddRange(proof1, proof2);
        await db.SaveChangesAsync(cancellationToken);

        // Now transition both care requests to PaymentReported.
        pr1.ReportPayment(proof1.Id, march8.AddDays(2).AddHours(1));
        pr2.ReportPayment(proof2.Id, march9.AddDays(2).AddHours(1));

        await db.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 10. Pending nurse profiles
    // ─────────────────────────────────────────────────────────────────────────────────
    // "Pending" is represented as: User.ProfileType == NURSE AND Nurse.IsActive == false.
    // The pending-list endpoint (GET /api/admin/nurse-profiles/pending) queries:
    //   u.ProfileType == NURSE && u.NurseProfile != null && !u.NurseProfile.IsActive
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedPendingNurseProfilesAsync(
        NursingCareDbContext db, CancellationToken cancellationToken)
    {
        var pendingIds = new[] { PendingNurse1Id, PendingNurse2Id, PendingNurse3Id };
        var existingIds = await db.Users
            .Where(u => pendingIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToHashSetAsync(cancellationToken);

        if (existingIds.Count == pendingIds.Length)
        {
            return; // already seeded
        }

        var nurseRole = await db.Roles.SingleAsync(r => r.Name == SystemRoles.Nurse, cancellationToken);

        var pendingData = new[]
        {
            (Id: PendingNurse1Id, Name: "Raquel",  LastName: "Familia Perez",   Phone: "8095560023", Cedula: "40234500023", Email: "raquel.familia@nurses.test",  License: "200023", Specialty: "Cuidado de adultos"),
            (Id: PendingNurse2Id, Name: "Xiomara", LastName: "Reyes Sanchez",   Phone: "8295560024", Cedula: "40234500024", Email: "xiomara.reyes@nurses.test",   License: "200024", Specialty: "Cuidado geriatrico"),
            (Id: PendingNurse3Id, Name: "Patricia",LastName: "Gomez Arias",     Phone: "8495560025", Cedula: "40234500025", Email: "patricia.gomez@nurses.test",  License: "200025", Specialty: "Cuidado pediatrico"),
        };

        foreach (var pd in pendingData)
        {
            if (existingIds.Contains(pd.Id))
            {
                continue;
            }

            var user = new User
            {
                Id = pd.Id,
                Email = pd.Email,
                ProfileType = UserProfileType.NURSE,
                Name = pd.Name,
                LastName = pd.LastName,
                DisplayName = $"{pd.Name} {pd.LastName}",
                IdentificationNumber = pd.Cedula,
                Phone = pd.Phone,
                PasswordHash = HashPassword("12345678"),
                IsActive = true,   // User account is active; nurse profile is pending review.
                CreatedAtUtc = DateTime.UtcNow,
                FailedLoginAttemptCount = 0,
                ResetPasswordFailedAttemptCount = 0,
            };

            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = nurseRole.Id,
                Role = nurseRole,
            });

            db.Users.Add(user);

            // Nurse.IsActive = false marks the profile as pending admin review.
            db.Nurses.Add(new Nurse
            {
                UserId = pd.Id,
                IsActive = false,          // <-- this is what the pending query filters on
                HireDate = null,           // not yet hired/approved
                Specialty = pd.Specialty,
                LicenseId = pd.License,
                BankName = null,
                AccountNumber = null,
                Category = "Junior",
                VisitDailyRate = 1700m,
                HomeCareMonthlyRate = 30000m,
                HomeCareMonthlyExpectedDays = 23.83m,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 11. Scheduled deduction lifecycle states (Cancelled + Completed)
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedScheduledDeductionLifecycleStatesAsync(
        NursingCareDbContext db, CancellationToken cancellationToken)
    {
        var adminId  = CatalogSeeding.SeededAdminId;
        var feb1     = new DateOnly(2026, 2, 1);
        var now      = new DateTime(2026, 3, 31, 12, 0, 0, DateTimeKind.Utc);

        // (a) Cancelled plan — Zoila: a recurring health-plan that was cancelled.
        var cancelled = ScheduledDeduction.CreateRecurring(
            nurseUserId:       CatalogSeeding.NurseIds["Zoila"],
            deductionType:     DeductionType.Insurance,
            label:             "Seguro Médico cancelado",
            recurringAmount:   400m,
            cadence:           DeductionCadence.Monthly,
            startPeriodDate:   feb1,
            endDate:           null,
            maxOccurrences:    null,
            notes:             "Plan cancelado por solicitud de la enfermera.",
            createdByUserId:   adminId,
            createdAtUtc:      now.AddDays(-30));
        cancelled.SyncGeneratedCount(1);
        cancelled.Cancel(adminId, "Solicitud de baja voluntaria del plan de seguro.", now);

        // (b) Completed plan — Maria Isabel: amortizing advance fully settled.
        // Principal 3,000 in 3 installments of 1,000 — settle all 3 so it completes.
        var completed = ScheduledDeduction.CreateAmortizing(
            nurseUserId:          CatalogSeeding.NurseIds["Maria Isabel"],
            deductionType:        DeductionType.Advance,
            label:                "Adelanto por emergencia — saldado",
            principalAmount:      3000m,
            interestRatePercent:  0m,
            totalInstallments:    3,
            cadence:              DeductionCadence.Monthly,
            startPeriodDate:      new DateOnly(2026, 1, 1),
            notes:                "Adelanto de emergencia familiar. Saldado en 3 cuotas.",
            createdByUserId:      adminId,
            createdAtUtc:         now.AddDays(-90));
        completed.SyncGeneratedCount(3);
        completed.ApplySettlement(3, 3000m, now); // fully settled → Status = Completed

        db.ScheduledDeductions.AddRange(cancelled, completed);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // 12. Future scheduled care requests (Approved + Pending)
    // ─────────────────────────────────────────────────────────────────────────────────
    // Represents upcoming scheduled visits in the next 1-2 months. These are NOT
    // completed, so they generate no payroll lines.
    // ─────────────────────────────────────────────────────────────────────────────────

    private static async Task SeedFutureScheduledRequestsAsync(
        NursingCareDbContext db, CancellationToken cancellationToken)
    {
        // Use a date anchor in the future relative to DateTime.UtcNow.
        var future1 = DateTime.UtcNow.AddDays(10).Date.ToUniversalTime().AddHours(8);
        var future2 = DateTime.UtcNow.AddDays(14).Date.ToUniversalTime().AddHours(8);
        var future3 = DateTime.UtcNow.AddDays(21).Date.ToUniversalTime().AddHours(8);
        var future4 = DateTime.UtcNow.AddDays(28).Date.ToUniversalTime().AddHours(8);
        var future5 = DateTime.UtcNow.AddDays(35).Date.ToUniversalTime().AddHours(8);
        var future6 = DateTime.UtcNow.AddDays(42).Date.ToUniversalTime().AddHours(8);

        // Nurses distributed across the roster for variety in upcoming visits.
        var n12 = CatalogSeeding.NurseIds["Annie"];
        var n13 = CatalogSeeding.NurseIds["Zoila"];
        var n14 = CatalogSeeding.NurseIds["Maria Isabel"];
        var n15 = CatalogSeeding.NurseIds["Emilina"];

        var futureRequests = new[]
        {
            // Approved — 4 upcoming scheduled visits, nurse assigned.
            MakeApproved(ClientBeatrizId, n12, "Cuidado domiciliario programado — turno dia",
                "domicilio_dia_12h", "medio_dia",   4, "local",  "estandar", 10000m, future1),
            MakeApproved(ClientCarmenId,  n13, "Rehabilitacion hogar post-cirugia programada",
                "hogar_diario",     "dia_completo", 5, "cercana","moderada", 13750m, future2),
            MakeApproved(ClientAnaId,     n14, "Sonda vesical programada",
                "sonda_vesical",    "sesion",       1, "local",  "estandar",  2000m, future3),
            MakeApproved(ClientLuisaId,   n15, "Atención domiciliaria nocturna programada",
                "domicilio_noche_12h","medio_dia",  3, "media",  "moderada",  9900m, future4),

            // Pending — no nurse assigned yet.
            Make(ClientRosaId, null, "Cuidado post-operatorio programado — sin asignar",
                "hogar_diario",     "dia_completo", 6, "local",  "estandar", 15000m, future5),
            Make(CatalogSeeding.TestClientId, null, "Suero intravenoso programado — pendiente enfermera",
                "suero",            "sesion",       2, "cercana","estandar",  4400m, future6),
        };

        db.CareRequests.AddRange(futureRequests);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // Password hashing — same algorithm as CatalogSeeding
    // ─────────────────────────────────────────────────────────────────────────────────

    private static string HashPassword(string password)
    {
        const int saltSize = 16;
        const int hashSize = 32;
        const int iterations = 10000;
        var algorithm = System.Security.Cryptography.HashAlgorithmName.SHA256;

        byte[] salt = new byte[saltSize];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        byte[] hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, algorithm, hashSize);

        byte[] hashWithSalt = new byte[saltSize + hashSize];
        Array.Copy(salt, 0, hashWithSalt, 0, saltSize);
        Array.Copy(hash, 0, hashWithSalt, saltSize, hashSize);

        return Convert.ToBase64String(hashWithSalt);
    }
}

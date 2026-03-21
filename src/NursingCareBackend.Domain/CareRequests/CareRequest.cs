using System.Collections.Immutable;

namespace NursingCareBackend.Domain.CareRequests;

public sealed class CareRequest
{
    public Guid Id { get; private set; }

    // UC003: User is the owner used for volume discounts.
    public Guid UserID { get; private set; }

    public string Description { get; private set; } = default!;
    public string? CareRequestReason { get; private set; }

    // UC003 pricing fields
    public string CareRequestType { get; private set; } = default!;
    public string UnitType { get; private set; } = default!;
    public int Unit { get; private set; }
    public decimal Price { get; private set; } // effective base price used
    public decimal Total { get; private set; } // grand total (total + medical supplies)
    public string? DistanceFactor { get; private set; }
    public string? ComplexityLevel { get; private set; }
    public decimal? ClientBasePrice { get; private set; } // override base price (only used when provided > 0)
    public decimal? MedicalSuppliesCost { get; private set; } // additive; only for medical services
    public DateOnly? CareRequestDate { get; private set; }

    // Not used by the pricing algorithm, but included for completeness of UC003.
    public string? SuggestedNurse { get; private set; }
    public Guid? AssignedNurse { get; private set; }

    public CareRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    // --- Static pricing catalogs (from documentation) ---

    public sealed record CareRequestTypeInfo(string Category, decimal BasePrice, string UnitType);

    public static readonly ImmutableDictionary<string, CareRequestTypeInfo> CareRequestTypes =
        new Dictionary<string, CareRequestTypeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["hogar_diario"]        = new("hogar",     2500m,  "dia_completo"),
            ["hogar_basico"]        = new("hogar",    55000m,  "mes"),
            ["hogar_estandar"]      = new("hogar",    60000m,  "mes"),
            ["hogar_premium"]       = new("hogar",    65000m,  "mes"),
            ["domicilio_dia_12h"]   = new("domicilio", 2500m,  "medio_dia"),
            ["domicilio_noche_12h"] = new("domicilio", 2500m,  "medio_dia"),
            ["domicilio_24h"]       = new("domicilio", 3500m,  "dia_completo"),
            ["suero"]               = new("medicos",  2000m,  "sesion"),
            ["medicamentos"]        = new("medicos",  2000m,  "sesion"),
            ["sonda_vesical"]       = new("medicos",  2000m,  "sesion"),
            ["sonda_nasogastrica"]  = new("medicos",  3000m,  "sesion"),
            ["sonda_peg"]           = new("medicos",  4000m,  "sesion"),
            ["curas"]               = new("medicos",  2000m,  "sesion"),
        }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, decimal> CategoryComplexity =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["hogar"]     = 1.0m,
            ["domicilio"] = 1.2m,
            ["medicos"]   = 1.5m,
        }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, decimal> DistanceFactors =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["local"]   = 1.0m,
            ["cercana"] = 1.1m,
            ["media"]   = 1.2m,
            ["lejana"]  = 1.3m,
        }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, decimal> ComplexityFactors =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["estandar"] = 1.0m,
            ["moderada"] = 1.1m,
            ["alta"]     = 1.2m,
            ["critica"]  = 1.3m,
        }.ToImmutableDictionary();

    // Key = minimum count, Value = discount percentage.
    public static readonly ImmutableSortedDictionary<int, int> VolumeDiscounts =
        ImmutableSortedDictionary<int, int>.Empty
            .Add(1, 0)
            .Add(5, 5)
            .Add(10, 10)
            .Add(20, 15)
            .Add(50, 20);

    private CareRequest() { } // For ORM

    private CareRequest(
        Guid userID,
        string description,
        string? careRequestReason,
        string careRequestType,
        string? suggestedNurse,
        Guid? assignedNurse,
        int unit,
        decimal? price,
        decimal? clientBasePrice,
        string? distanceFactor,
        string? complexityLevel,
        decimal? medicalSuppliesCost,
        DateOnly? careRequestDate,
        int existingSameUnitTypeCount,
        DateTime createdAtUtc)
    {
        if (userID == Guid.Empty)
            throw new ArgumentException("UserID cannot be empty.", nameof(userID));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        if (string.IsNullOrWhiteSpace(careRequestType))
            throw new ArgumentException("CareRequestType is required.", nameof(careRequestType));

        if (!CareRequestTypes.ContainsKey(careRequestType))
            throw new ArgumentException($"Unknown care_request_type '{careRequestType}'.", nameof(careRequestType));

        if (unit <= 0)
            throw new ArgumentException("Unit must be greater than zero.", nameof(unit));

        if (clientBasePrice is { } cbp && cbp <= 0)
            throw new ArgumentException("ClientBasePrice must be > 0 when provided.", nameof(clientBasePrice));

        if (price is { } p && p <= 0)
            throw new ArgumentException("Price must be > 0 when provided.", nameof(price));

        if (medicalSuppliesCost is { } msc && msc < 0)
            throw new ArgumentException("MedicalSuppliesCost must be >= 0 when provided.", nameof(medicalSuppliesCost));

        Id = Guid.NewGuid();
        UserID = userID;
        Description = description;
        CareRequestReason = careRequestReason;

        SuggestedNurse = suggestedNurse;
        AssignedNurse = assignedNurse;

        CareRequestType = careRequestType;
        var info = CareRequestTypes[careRequestType];
        UnitType = info.UnitType;
        Unit = unit;
        ClientBasePrice = clientBasePrice;
        MedicalSuppliesCost = medicalSuppliesCost;
        CareRequestDate = careRequestDate;

        SetCareRequestDefaults(distanceFactor, complexityLevel);
        CalculateTotals(existingSameUnitTypeCount, price);

        Status = CareRequestStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private void SetCareRequestDefaults(string? distanceFactor, string? complexityLevel)
    {
        var info = CareRequestTypes[CareRequestType];

        // Defaults for domicilio.
        if (string.Equals(info.Category, "domicilio", StringComparison.OrdinalIgnoreCase))
        {
            DistanceFactor = string.IsNullOrWhiteSpace(distanceFactor) ? "local" : distanceFactor;
        }
        else
        {
            DistanceFactor = null;
        }

        // Defaults for hogar/domicilio.
        if (string.Equals(info.Category, "hogar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(info.Category, "domicilio", StringComparison.OrdinalIgnoreCase))
        {
            ComplexityLevel = string.IsNullOrWhiteSpace(complexityLevel) ? "estandar" : complexityLevel;
        }
        else
        {
            ComplexityLevel = null;
        }

        if (!MedicalSuppliesCost.HasValue)
            MedicalSuppliesCost = 0m;
    }

    private void CalculateTotals(int existingSameUnitTypeCount, decimal? providedPrice)
    {
        var info = CareRequestTypes[CareRequestType];

        // Step 1: Determine base price (provided price > client override > catalog).
        var basePrice = (providedPrice is { } p && p > 0)
            ? p
            : (ClientBasePrice is { } cbp && cbp > 0 ? cbp : info.BasePrice);

        if (basePrice <= 0)
            basePrice = 60m;

        // Step 2: Category factor.
        var category = info.Category;
        CategoryComplexity.TryGetValue(category, out var categoryFactor);
        if (categoryFactor <= 0) categoryFactor = 1.0m;

        // Step 3: Distance factor.
        var distanceFactor = 1.0m;
        if (string.Equals(category, "domicilio", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(DistanceFactor) ||
                !DistanceFactors.TryGetValue(DistanceFactor, out distanceFactor))
                distanceFactor = 1.0m;
        }

        // Step 4: Complexity factor.
        var complexityFactor = 1.0m;
        if (string.Equals(category, "hogar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "domicilio", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(ComplexityLevel) &&
                ComplexityFactors.TryGetValue(ComplexityLevel, out var cf))
            {
                complexityFactor = cf;
            }
        }

        // Step 5: Volume discount.
        var volumeDiscountPercent = CalculateVolumeDiscount(existingSameUnitTypeCount);

        // Step 6: Unit price.
        var unitPrice = basePrice
            * categoryFactor
            * distanceFactor
            * complexityFactor
            * (1 - volumeDiscountPercent / 100m);

        // Step 7: Total before supplies.
        var total = unitPrice * Unit;

        // Step 8: Add medical supplies.
        var supplies = MedicalSuppliesCost ?? 0m;
        var grandTotal = total + supplies;

        if (grandTotal < 0)
            throw new InvalidOperationException("Calculated total cannot be negative.");

        Price = decimal.Round(basePrice, 2, MidpointRounding.AwayFromZero);
        Total = decimal.Round(grandTotal, 2, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static int CalculateVolumeDiscount(int existingSameUnitTypeCount)
    {
        if (existingSameUnitTypeCount <= 0) return 0;

        var applicable = 0;
        foreach (var kvp in VolumeDiscounts)
        {
            if (existingSameUnitTypeCount >= kvp.Key)
                applicable = kvp.Value;
        }
        return applicable;
    }

    public static string GetUnitTypeForCareRequestType(string careRequestType)
    {
        if (!CareRequestTypes.TryGetValue(careRequestType, out var info))
            throw new ArgumentException($"Unknown care_request_type '{careRequestType}'.", nameof(careRequestType));

        return info.UnitType;
    }

    public static CareRequest Create(
        Guid userID,
        string description,
        string? careRequestReason,
        string careRequestType,
        string? suggestedNurse,
        Guid? assignedNurse,
        int unit,
        decimal? price,
        decimal? clientBasePrice,
        string? distanceFactor,
        string? complexityLevel,
        decimal? medicalSuppliesCost,
        DateOnly? careRequestDate,
        int existingSameUnitTypeCount)
    {
        return new CareRequest(
            userID: userID,
            description: description,
            careRequestReason: careRequestReason,
            careRequestType: careRequestType,
            suggestedNurse: suggestedNurse,
            assignedNurse: assignedNurse,
            unit: unit,
            price: price,
            clientBasePrice: clientBasePrice,
            distanceFactor: distanceFactor,
            complexityLevel: complexityLevel,
            medicalSuppliesCost: medicalSuppliesCost,
            careRequestDate: careRequestDate,
            existingSameUnitTypeCount: existingSameUnitTypeCount,
            createdAtUtc: DateTime.UtcNow);
    }

    public void Approve(DateTime transitionedAtUtc)
    {
        EnsurePending(nameof(Approve));

        if (!AssignedNurse.HasValue)
        {
            throw new InvalidOperationException("Care request must have an assigned nurse before approval.");
        }

        Status = CareRequestStatus.Approved;
        ApprovedAtUtc = transitionedAtUtc;
        UpdatedAtUtc = transitionedAtUtc;
    }

    public void Reject(DateTime transitionedAtUtc)
    {
        EnsurePending(nameof(Reject));

        Status = CareRequestStatus.Rejected;
        RejectedAtUtc = transitionedAtUtc;
        UpdatedAtUtc = transitionedAtUtc;
    }

    public void Complete(DateTime transitionedAtUtc, Guid nurseUserId)
    {
        if (Status != CareRequestStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Care request can only be completed from Approved status. Current status is {Status}.");
        }

        if (!AssignedNurse.HasValue)
        {
            throw new InvalidOperationException("Care request must have an assigned nurse before completion.");
        }

        if (AssignedNurse.Value != nurseUserId)
        {
            throw new InvalidOperationException("Only the assigned nurse can complete this care request.");
        }

        var completionDate = DateOnly.FromDateTime(transitionedAtUtc);
        if (CareRequestDate.HasValue && CareRequestDate.Value > completionDate)
        {
            throw new InvalidOperationException("Care request cannot be completed before its scheduled care-request date.");
        }

        Status = CareRequestStatus.Completed;
        CompletedAtUtc = transitionedAtUtc;
        UpdatedAtUtc = transitionedAtUtc;
    }

    public void AssignNurse(Guid nurseUserId, DateTime assignedAtUtc)
    {
        if (nurseUserId == Guid.Empty)
        {
            throw new ArgumentException("Assigned nurse cannot be empty.", nameof(nurseUserId));
        }

        AssignedNurse = nurseUserId;
        UpdatedAtUtc = assignedAtUtc;
    }

    private void EnsurePending(string actionName)
    {
        if (Status != CareRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Care request can only be {actionName.ToLowerInvariant()}d from Pending status. Current status is {Status}.");
        }
    }
}

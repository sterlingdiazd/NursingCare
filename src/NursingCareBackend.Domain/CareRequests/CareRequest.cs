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

    // Immutable snapshot of pricing at creation time (catalog-independent).
    public string? PricingCategoryCode { get; private set; }
    public decimal? CategoryFactorSnapshot { get; private set; }
    public decimal? DistanceFactorMultiplierSnapshot { get; private set; }
    public decimal? ComplexityMultiplierSnapshot { get; private set; }
    public int? VolumeDiscountPercentSnapshot { get; private set; }
    public decimal? LineBeforeVolumeDiscount { get; private set; }
    public decimal? UnitPriceAfterVolumeDiscount { get; private set; }
    public decimal? SubtotalBeforeSupplies { get; private set; }

    // Not used by the pricing algorithm, but included for completeness of UC003.
    public string? SuggestedNurse { get; private set; }
    public Guid? AssignedNurse { get; private set; }

    public CareRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    // Service lifecycle billing fields
    public string? InvoiceNumber { get; private set; }
    public DateTime? InvoicedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }
    public bool IsVoided => VoidedAtUtc.HasValue;

    private CareRequest() { } // For ORM

    private CareRequest(
        Guid userID,
        string description,
        string? careRequestReason,
        string careRequestType,
        string unitType,
        string? suggestedNurse,
        Guid? assignedNurse,
        int unit,
        decimal price,
        decimal total,
        decimal? clientBasePrice,
        string? distanceFactor,
        string? complexityLevel,
        decimal? medicalSuppliesCost,
        DateOnly? careRequestDate,
        string pricingCategoryCode,
        decimal categoryFactorSnapshot,
        decimal distanceFactorMultiplierSnapshot,
        decimal complexityMultiplierSnapshot,
        int volumeDiscountPercentSnapshot,
        decimal? lineBeforeVolumeDiscount,
        decimal? unitPriceAfterVolumeDiscount,
        decimal? subtotalBeforeSupplies,
        DateTime createdAtUtc)
    {
        if (userID == Guid.Empty)
            throw new ArgumentException("UserID cannot be empty.", nameof(userID));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        if (string.IsNullOrWhiteSpace(careRequestType))
            throw new ArgumentException("CareRequestType is required.", nameof(careRequestType));

        if (string.IsNullOrWhiteSpace(unitType))
            throw new ArgumentException("UnitType is required.", nameof(unitType));

        if (unit <= 0)
            throw new ArgumentException("Unit must be greater than zero.", nameof(unit));

        if (clientBasePrice is { } cbp && cbp <= 0)
            throw new ArgumentException("ClientBasePrice must be > 0 when provided.", nameof(clientBasePrice));

        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero.", nameof(price));

        if (total < 0)
            throw new ArgumentException("Total cannot be negative.", nameof(total));

        if (medicalSuppliesCost is { } msc && msc < 0)
            throw new ArgumentException("MedicalSuppliesCost must be >= 0 when provided.", nameof(medicalSuppliesCost));

        if (string.IsNullOrWhiteSpace(pricingCategoryCode))
            throw new ArgumentException("PricingCategoryCode is required.", nameof(pricingCategoryCode));

        var snapshotNonNullCount = (lineBeforeVolumeDiscount.HasValue ? 1 : 0)
            + (unitPriceAfterVolumeDiscount.HasValue ? 1 : 0)
            + (subtotalBeforeSupplies.HasValue ? 1 : 0);
        if (snapshotNonNullCount > 0 && snapshotNonNullCount < 3)
            throw new ArgumentException(
                "LineBeforeVolumeDiscount, UnitPriceAfterVolumeDiscount, and SubtotalBeforeSupplies must all be provided or all be null.",
                nameof(lineBeforeVolumeDiscount));

        Id = Guid.NewGuid();
        UserID = userID;
        Description = description;
        CareRequestReason = careRequestReason;

        SuggestedNurse = suggestedNurse;
        AssignedNurse = assignedNurse;

        CareRequestType = careRequestType;
        UnitType = unitType;
        Unit = unit;
        ClientBasePrice = clientBasePrice;
        MedicalSuppliesCost = medicalSuppliesCost;
        CareRequestDate = careRequestDate;

        DistanceFactor = distanceFactor;
        ComplexityLevel = complexityLevel;

        Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        Total = decimal.Round(total, 2, MidpointRounding.AwayFromZero);

        PricingCategoryCode = pricingCategoryCode;
        CategoryFactorSnapshot = categoryFactorSnapshot;
        DistanceFactorMultiplierSnapshot = distanceFactorMultiplierSnapshot;
        ComplexityMultiplierSnapshot = complexityMultiplierSnapshot;
        VolumeDiscountPercentSnapshot = volumeDiscountPercentSnapshot;
        LineBeforeVolumeDiscount = lineBeforeVolumeDiscount.HasValue
            ? decimal.Round(lineBeforeVolumeDiscount.Value, 4, MidpointRounding.AwayFromZero)
            : null;
        UnitPriceAfterVolumeDiscount = unitPriceAfterVolumeDiscount.HasValue
            ? decimal.Round(unitPriceAfterVolumeDiscount.Value, 4, MidpointRounding.AwayFromZero)
            : null;
        SubtotalBeforeSupplies = subtotalBeforeSupplies.HasValue
            ? decimal.Round(subtotalBeforeSupplies.Value, 2, MidpointRounding.AwayFromZero)
            : null;

        Status = CareRequestStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static CareRequest Create(CareRequestCreateParams p)
    {
        return new CareRequest(
            userID: p.UserID,
            description: p.Description,
            careRequestReason: p.CareRequestReason,
            careRequestType: p.CareRequestType,
            unitType: p.UnitType,
            suggestedNurse: p.SuggestedNurse,
            assignedNurse: p.AssignedNurse,
            unit: p.Unit,
            price: p.Price,
            total: p.Total,
            clientBasePrice: p.ClientBasePrice,
            distanceFactor: p.DistanceFactor,
            complexityLevel: p.ComplexityLevel,
            medicalSuppliesCost: p.MedicalSuppliesCost,
            careRequestDate: p.CareRequestDate,
            pricingCategoryCode: p.PricingCategoryCode,
            categoryFactorSnapshot: p.CategoryFactorSnapshot,
            distanceFactorMultiplierSnapshot: p.DistanceFactorMultiplierSnapshot,
            complexityMultiplierSnapshot: p.ComplexityMultiplierSnapshot,
            volumeDiscountPercentSnapshot: p.VolumeDiscountPercentSnapshot,
            lineBeforeVolumeDiscount: p.LineBeforeVolumeDiscount,
            unitPriceAfterVolumeDiscount: p.UnitPriceAfterVolumeDiscount,
            subtotalBeforeSupplies: p.SubtotalBeforeSupplies,
            createdAtUtc: p.CreatedAtUtc);
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

    public void Reject(DateTime transitionedAtUtc, string? reason = null)
    {
        EnsurePending(nameof(Reject));

        Status = CareRequestStatus.Rejected;
        RejectedAtUtc = transitionedAtUtc;
        RejectionReason = reason;
        UpdatedAtUtc = transitionedAtUtc;
    }

    public void Cancel(DateTime transitionedAtUtc)
    {
        if (Status != CareRequestStatus.Pending && Status != CareRequestStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Care request can only be cancelled from Pending or Approved status. Current status is {Status}.");
        }

        Status = CareRequestStatus.Cancelled;
        CancelledAtUtc = transitionedAtUtc;
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

    public void Invoice(string invoiceNumber, DateTime invoiceDate)
    {
        if (Status != CareRequestStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Care request can only be invoiced from Completed status. Current status is {Status}.");
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number cannot be empty.", nameof(invoiceNumber));

        InvoiceNumber = invoiceNumber;
        Status = CareRequestStatus.Invoiced;
        InvoicedAtUtc = invoiceDate;
        UpdatedAtUtc = invoiceDate;
    }

    public void Pay(string bankReference, DateTime paymentDate)
    {
        if (Status != CareRequestStatus.Invoiced)
        {
            throw new InvalidOperationException(
                $"Care request can only be paid from Invoiced status. Current status is {Status}.");
        }

        if (string.IsNullOrWhiteSpace(bankReference))
            throw new ArgumentException("Bank reference cannot be empty.", nameof(bankReference));

        Status = CareRequestStatus.Paid;
        PaidAtUtc = paymentDate;
        UpdatedAtUtc = paymentDate;
    }

    public void Void(string voidReason, DateTime voidedAtUtc)
    {
        if (Status != CareRequestStatus.Completed && Status != CareRequestStatus.Invoiced)
        {
            throw new InvalidOperationException(
                $"Care request can only be voided from Completed or Invoiced status. Current status is {Status}.");
        }

        if (string.IsNullOrWhiteSpace(voidReason))
            throw new ArgumentException("Void reason cannot be empty.", nameof(voidReason));

        Status = CareRequestStatus.Voided;
        VoidReason = voidReason;
        VoidedAtUtc = voidedAtUtc;
        UpdatedAtUtc = voidedAtUtc;
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

using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

/// <summary>T2.2 — payment-reminder idempotency stamps and the IsPaymentOwed gate.</summary>
public class PaymentReminderTests
{
    [Fact]
    public void MarkPaymentDueReminderSent_SetsTimestamp_Once()
    {
        var r = Completed();
        Assert.Null(r.PaymentDueReminderSentAtUtc);

        var at = new DateTime(2026, 5, 26, 10, 0, 0, DateTimeKind.Utc);
        r.MarkPaymentDueReminderSent(at);

        Assert.Equal(at, r.PaymentDueReminderSentAtUtc);
    }

    [Fact]
    public void MarkPaymentOverdueReminderSent_SetsTimestamp()
    {
        var r = Completed();
        var at = new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);

        r.MarkPaymentOverdueReminderSent(at);

        Assert.Equal(at, r.PaymentOverdueReminderSentAtUtc);
    }

    [Fact]
    public void IsPaymentOwed_TrueForCompletedAndInvoiced_FalseOncePaid()
    {
        var r = Completed();
        Assert.True(r.IsPaymentOwed); // Completed → owed

        r.Invoice("FAC-1", DateTime.UtcNow);
        Assert.True(r.IsPaymentOwed); // Invoiced → owed

        r.Pay("TRF-1", DateTime.UtcNow);
        Assert.False(r.IsPaymentOwed); // Paid → not owed
    }

    [Fact]
    public void IsPaymentOwed_FalseWhenPending()
    {
        var r = CareRequest.Create(Params());
        Assert.False(r.IsPaymentOwed);
    }

    private static CareRequest Completed()
    {
        var nurse = Guid.NewGuid();
        var r = CareRequest.Create(Params(nurse));
        r.Approve(DateTime.UtcNow.AddDays(-2));
        r.Complete(DateTime.UtcNow.AddDays(-1), nurse);
        return r;
    }

    private static CareRequestCreateParams Params(Guid? nurse = null) => new()
    {
        UserID = Guid.NewGuid(),
        Description = "Servicio",
        CareRequestReason = null,
        CareRequestType = "domicilio_24h",
        UnitType = "dia_completo",
        SuggestedNurse = null,
        AssignedNurse = nurse,
        Unit = 1,
        Price = 3500m,
        Total = 4200m,
        ClientBasePrice = null,
        DistanceFactor = "local",
        ComplexityLevel = "estandar",
        MedicalSuppliesCost = null,
        CareRequestDate = null,
        PricingCategoryCode = "domicilio",
        CategoryFactorSnapshot = 1.2m,
        DistanceFactorMultiplierSnapshot = 1.0m,
        ComplexityMultiplierSnapshot = 1.0m,
        VolumeDiscountPercentSnapshot = 0,
        LineBeforeVolumeDiscount = null,
        UnitPriceAfterVolumeDiscount = null,
        SubtotalBeforeSupplies = null,
        CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
    };
}

using NursingCareBackend.Domain.Payroll;
using Xunit;

namespace NursingCareBackend.Domain.Tests;

public class NursePeriodPaymentTests
{
    private static readonly DateTime Now = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Period = Guid.NewGuid();
    private static readonly Guid Nurse = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private static NursePeriodPayment Confirmed(string? bankRef = "REF-1") =>
        NursePeriodPayment.Create(Period, Nurse, Admin, bankRef, Now);

    [Fact]
    public void Create_Sets_Confirmed_And_Pending_Delivery()
    {
        var p = Confirmed();

        Assert.Equal(NursePaymentStatus.Confirmed, p.PaymentStatus);
        Assert.Equal(VoucherDeliveryStatus.Pending, p.VoucherDeliveryStatus);
        Assert.Equal("REF-1", p.BankReference);
        Assert.Equal(Admin, p.StatusChangedByUserId);
        Assert.Equal(Now, p.StatusChangedAtUtc);
        Assert.Null(p.PaymentStatusReason);
    }

    [Fact]
    public void Reconfirm_Returns_To_Confirmed_And_Resets_Delivery()
    {
        var p = Confirmed();
        p.MarkVoucherFailed("smtp", Now);

        p.Reconfirm(Admin, "REF-2", Now.AddMinutes(5));

        Assert.Equal(NursePaymentStatus.Confirmed, p.PaymentStatus);
        Assert.Equal(VoucherDeliveryStatus.Pending, p.VoucherDeliveryStatus);
        Assert.Equal("REF-2", p.BankReference);
        Assert.Null(p.PaymentStatusReason);
    }

    [Fact]
    public void MarkPaymentFailed_From_Confirmed_Sets_Failed_With_Reason()
    {
        var p = Confirmed();

        p.MarkPaymentFailed("Cuenta inválida", Admin, Now.AddHours(1));

        Assert.Equal(NursePaymentStatus.Failed, p.PaymentStatus);
        Assert.Equal("Cuenta inválida", p.PaymentStatusReason);
    }

    [Fact]
    public void MarkPaymentFailed_Requires_Reason()
    {
        var p = Confirmed();
        Assert.Throws<ArgumentException>(() => p.MarkPaymentFailed("  ", Admin, Now));
    }

    [Fact]
    public void Reverse_Only_From_Confirmed()
    {
        var p = Confirmed();
        p.MarkPaymentFailed("bounce", Admin, Now);

        // Now Failed -> Reverse is not allowed.
        Assert.Throws<InvalidOperationException>(() => p.Reverse("motivo", Admin, Now));
    }

    [Fact]
    public void Reverse_From_Confirmed_Sets_Reversed_With_Reason()
    {
        var p = Confirmed();

        p.Reverse("Pago duplicado", Admin, Now.AddHours(2));

        Assert.Equal(NursePaymentStatus.Reversed, p.PaymentStatus);
        Assert.Equal("Pago duplicado", p.PaymentStatusReason);
    }

    [Fact]
    public void ResetForReopen_From_Confirmed_Goes_Pending_And_Keeps_BankReference()
    {
        var p = Confirmed("REF-KEEP");

        p.ResetForReopen(Admin, Now.AddDays(1));

        Assert.Equal(NursePaymentStatus.Pending, p.PaymentStatus);
        Assert.Equal("REF-KEEP", p.BankReference); // preserved for re-confirm
    }

    [Fact]
    public void ResetForReopen_Is_NoOp_When_Reversed()
    {
        var p = Confirmed();
        p.Reverse("motivo", Admin, Now);

        p.ResetForReopen(Admin, Now.AddDays(1));

        Assert.Equal(NursePaymentStatus.Reversed, p.PaymentStatus); // unchanged
    }
}

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;
using NursingCareBackend.Application.AdminPortal.Payroll.Commands.DeliverPeriodVouchers;
using NursingCareBackend.Application.Exceptions;

namespace NursingCareBackend.Application.Tests;

public sealed class DeliverPeriodVouchersHandlerTests
{
    private static readonly Guid PeriodId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private static PayrollVoucherData Nurse(string name) => new()
    {
        PeriodId = PeriodId,
        PeriodStartDate = new DateOnly(2026, 3, 1),
        PeriodEndDate = new DateOnly(2026, 3, 31),
        PaymentDate = new DateOnly(2026, 3, 31),
        PeriodStatus = "Closed",
        NurseUserId = Guid.NewGuid(),
        NurseDisplayName = name,
        Lines = [],
    };

    private static DeliverPeriodVouchersHandler Build(
        IReadOnlyList<PayrollVoucherData> nurses,
        IConfirmNursePeriodPaymentHandler confirmHandler,
        bool periodExists = true)
    {
        var repo = new FakeBulkRepository(nurses, periodExists);
        return new DeliverPeriodVouchersHandler(repo, new FakeScopeFactory(confirmHandler), NullLogger<DeliverPeriodVouchersHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Delivers_To_All_Nurses_And_Aggregates_Counts()
    {
        var n1 = Nurse("Ana"); var n2 = Nurse("Charleny"); var n3 = Nurse("Luisa");
        // n3 has no email => delivered=false but no exception.
        var confirm = new FakeConfirmHandler(cmd => Result(cmd, emailSent: cmd.NurseUserId != n3.NurseUserId));

        var handler = Build([n1, n2, n3], confirm);
        var result = await handler.Handle(new DeliverPeriodVouchersCommand(PeriodId, AdminId, "BATCH-REF-1"), default);

        result.TotalNurses.Should().Be(3);
        result.DeliveredCount.Should().Be(2);
        result.FailedCount.Should().Be(1);
        result.Items.Should().HaveCount(3);
        result.Items.Single(i => i.NurseUserId == n3.NurseUserId).VoucherEmailSent.Should().BeFalse();
        // The shared batch reference is forwarded to every per-nurse confirmation.
        confirm.Commands.Should().OnlyContain(c => c.BankReference == "BATCH-REF-1" && c.PeriodId == PeriodId && c.AdminUserId == AdminId);
        confirm.Commands.Select(c => c.NurseUserId).Should().BeEquivalentTo(new[] { n1.NurseUserId, n2.NurseUserId, n3.NurseUserId });
    }

    [Fact]
    public async Task Handle_Continues_When_One_Nurse_Throws()
    {
        var n1 = Nurse("Ana"); var n2 = Nurse("Charleny"); var n3 = Nurse("Luisa");
        var confirm = new FakeConfirmHandler(cmd =>
            cmd.NurseUserId == n2.NurseUserId
                ? throw new InvalidOperationException("boom")
                : Result(cmd, emailSent: true));

        var handler = Build([n1, n2, n3], confirm);
        var result = await handler.Handle(new DeliverPeriodVouchersCommand(PeriodId, AdminId, null), default);

        // The throwing nurse becomes a failed item; the batch still processes everyone.
        result.TotalNurses.Should().Be(3);
        result.DeliveredCount.Should().Be(2);
        result.FailedCount.Should().Be(1);
        var failed = result.Items.Single(i => i.NurseUserId == n2.NurseUserId);
        failed.VoucherEmailSent.Should().BeFalse();
        failed.VoucherDeliveryDetail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_Throws_When_Period_Missing()
    {
        var handler = Build([Nurse("Ana")], new FakeConfirmHandler(c => Result(c, true)), periodExists: false);
        await FluentActions.Awaiting(() => handler.Handle(new DeliverPeriodVouchersCommand(PeriodId, AdminId, null), default))
            .Should().ThrowAsync<VoucherNotFoundException>();
    }

    [Fact]
    public async Task Handle_Throws_When_No_Nurses_Have_Lines()
    {
        var handler = Build([], new FakeConfirmHandler(c => Result(c, true)));
        await FluentActions.Awaiting(() => handler.Handle(new DeliverPeriodVouchersCommand(PeriodId, AdminId, null), default))
            .Should().ThrowAsync<VoucherNotFoundException>();
    }

    private static ConfirmNursePeriodPaymentResult Result(ConfirmNursePeriodPaymentCommand cmd, bool emailSent) =>
        new(cmd.PeriodId, cmd.NurseUserId, DateTime.UtcNow, cmd.BankReference,
            VoucherEmailSent: emailSent,
            WhatsappUrl: emailSent ? "https://wa.me/18090000000?text=x" : string.Empty,
            RecipientLabel: emailSent ? "Comprobante enviado a la enfermera." : "El comprobante no se pudo enviar a la enfermera.",
            VoucherDeliveryDetail: emailSent ? "Comprobante enviado a la enfermera por correo." : "La enfermera no tiene un correo registrado.");
}

file sealed class FakeScopeFactory(IConfirmNursePeriodPaymentHandler handler) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new FakeScope(handler);
}

file sealed class FakeScope(IConfirmNursePeriodPaymentHandler handler) : IServiceScope
{
    public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(handler);
    public void Dispose() { }
}

file sealed class FakeServiceProvider(IConfirmNursePeriodPaymentHandler handler) : IServiceProvider
{
    public object? GetService(Type serviceType)
        => serviceType == typeof(IConfirmNursePeriodPaymentHandler) ? handler : null;
}

file sealed class FakeConfirmHandler : IConfirmNursePeriodPaymentHandler
{
    private readonly Func<ConfirmNursePeriodPaymentCommand, ConfirmNursePeriodPaymentResult> _behavior;
    public List<ConfirmNursePeriodPaymentCommand> Commands { get; } = [];

    public FakeConfirmHandler(Func<ConfirmNursePeriodPaymentCommand, ConfirmNursePeriodPaymentResult> behavior)
        => _behavior = behavior;

    public Task<ConfirmNursePeriodPaymentResult> Handle(ConfirmNursePeriodPaymentCommand command, CancellationToken cancellationToken)
    {
        Commands.Add(command);
        return Task.FromResult(_behavior(command)); // may throw, mirroring a real per-nurse failure
    }
}

file sealed class FakeBulkRepository : IAdminPayrollRepository
{
    private readonly IReadOnlyList<PayrollVoucherData> _nurses;
    private readonly bool _periodExists;

    public FakeBulkRepository(IReadOnlyList<PayrollVoucherData> nurses, bool periodExists)
    {
        _nurses = nurses;
        _periodExists = periodExists;
    }

    public Task<AdminPayrollPeriodDetail?> GetPeriodByIdAsync(Guid periodId, CancellationToken cancellationToken)
        => Task.FromResult(_periodExists
            ? new AdminPayrollPeriodDetail(periodId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
                new DateOnly(2026, 3, 29), new DateOnly(2026, 3, 31), "Closed", DateTime.UtcNow, DateTime.UtcNow,
                [], [], CanModify: false, ReopenedAtUtc: null, ReopenReason: null, ReopenCount: 0)
            : null);

    public Task<IReadOnlyList<PayrollVoucherData>> GetAllVoucherDataAsync(Guid periodId, CancellationToken cancellationToken)
        => Task.FromResult(_nurses);

    // Remaining interface members are not used by the batch delivery handler — throw to detect drift.
    public Task<PayrollVoucherData?> GetVoucherDataAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AdminPayrollPeriodListResult> GetPeriodsAsync(AdminPayrollPeriodListFilter filter, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid> CreatePeriodAsync(DateOnly startDate, DateOnly endDate, DateOnly cutoffDate, DateOnly paymentDate, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PeriodCloseResult> ClosePeriodAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PeriodCloseWarnings> GetCloseWarningsAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PeriodReopenResult> ReopenPeriodAsync(Guid periodId, string reason, Guid? reopenedByUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PeriodMutationResult> UpdatePeriodAsync(Guid periodId, DateOnly startDate, DateOnly endDate, DateOnly cutoffDate, DateOnly paymentDate, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PeriodMutationResult> DeletePeriodAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<AdminPayrollLineItem>> GetPeriodLinesAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AdminDeductionListResult> GetDeductionsAsync(Guid? nurseId, Guid? periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid> CreateDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> UpdateDeductionAsync(Guid deductionId, UpdateDeductionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> DeleteDeductionAsync(Guid deductionId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> SetDeductionPausedAsync(Guid deductionId, bool paused, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AdminCompensationAdjustmentListResult> GetAdjustmentsAsync(Guid? executionId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid> CreateAdjustmentAsync(CreateCompensationAdjustmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> UpdateAdjustmentAsync(Guid adjustmentId, UpdateCompensationAdjustmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> DeleteAdjustmentAsync(Guid adjustmentId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<NursePeriodHistoryItem>> GetNursePeriodHistoryAsync(Guid nurseId, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<int> CountNurseLinesInOpenPeriodsAsync(Guid nurseId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<int> CountNurseLinesInClosedPeriodsAsync(Guid nurseId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<NursePeriodDetail?> GetNursePeriodDetailAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken) => throw new NotImplementedException();
}

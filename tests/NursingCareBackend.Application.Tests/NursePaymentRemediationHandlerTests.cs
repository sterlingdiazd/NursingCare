using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.AdminPortal.Payroll.Commands.MarkNursePaymentFailed;
using NursingCareBackend.Application.AdminPortal.Payroll.Commands.ReverseNursePayment;
using NursingCareBackend.Application.Exceptions;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Application.Tests;

public sealed class NursePaymentRemediationHandlerTests
{
    private static readonly Guid Period = Guid.NewGuid();
    private static readonly Guid Nurse = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private static NursePeriodPayment ConfirmedPayment() =>
        NursePeriodPayment.Create(Period, Nurse, Admin, "REF-1", DateTime.UtcNow);

    [Fact]
    public async Task MarkFailed_Sets_Failed_Saves_And_Audits()
    {
        var repo = new FakePaymentRepo(ConfirmedPayment());
        var audit = new FakeAudit();
        var handler = new MarkNursePaymentFailedHandler(repo, audit);

        var result = await handler.Handle(
            new MarkNursePaymentFailedCommand(Period, Nurse, Admin, "Cuenta inválida"), default);

        result.PaymentStatus.Should().Be("Failed");
        repo.Saved.Should().BeTrue();
        audit.Records.Should().ContainSingle(r => r.Action == AdminAuditActions.NursePaymentFailed);
    }

    [Fact]
    public async Task Reverse_Sets_Reversed_Audits_And_Notifies_Nurse()
    {
        var repo = new FakePaymentRepo(ConfirmedPayment());
        var audit = new FakeAudit();
        var notify = new FakeUserNotifier();
        var handler = new ReverseNursePaymentHandler(repo, audit, notify, NullLogger<ReverseNursePaymentHandler>.Instance);

        var result = await handler.Handle(
            new ReverseNursePaymentCommand(Period, Nurse, Admin, "Pago duplicado"), default);

        result.PaymentStatus.Should().Be("Reversed");
        audit.Records.Should().ContainSingle(r => r.Action == AdminAuditActions.NursePaymentReversed);
        notify.Requests.Should().ContainSingle(r => r.RecipientUserId == Nurse && r.Category == "nurse_payment_reversed");
    }

    [Fact]
    public async Task Reverse_From_Failed_Returns_Conflict_Via_InvalidOperation()
    {
        var payment = ConfirmedPayment();
        payment.MarkPaymentFailed("bounce", Admin, DateTime.UtcNow);
        var handler = new ReverseNursePaymentHandler(new FakePaymentRepo(payment), new FakeAudit(), new FakeUserNotifier(), NullLogger<ReverseNursePaymentHandler>.Instance);

        await FluentActions
            .Awaiting(() => handler.Handle(new ReverseNursePaymentCommand(Period, Nurse, Admin, "x"), default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MarkFailed_Throws_NotFound_When_Missing()
    {
        var handler = new MarkNursePaymentFailedHandler(new FakePaymentRepo(null), new FakeAudit());
        await FluentActions
            .Awaiting(() => handler.Handle(new MarkNursePaymentFailedCommand(Period, Nurse, Admin, "x"), default))
            .Should().ThrowAsync<VoucherNotFoundException>();
    }
}

file sealed class FakePaymentRepo : INursePeriodPaymentRepository
{
    private readonly NursePeriodPayment? _payment;
    public bool Saved { get; private set; }
    public FakePaymentRepo(NursePeriodPayment? payment) => _payment = payment;

    public Task<NursePeriodPayment?> GetAsync(Guid payrollPeriodId, Guid nurseUserId, CancellationToken cancellationToken)
        => Task.FromResult(_payment);
    public Task AddAsync(NursePeriodPayment payment, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken cancellationToken) { Saved = true; return Task.CompletedTask; }
}

file sealed class FakeAudit : IAdminAuditService
{
    public List<AdminAuditRecord> Records { get; } = [];
    public Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }
}

file sealed class FakeUserNotifier : IUserNotificationPublisher
{
    public List<UserNotificationPublishRequest> Requests { get; } = [];
    public Task PublishToUserAsync(UserNotificationPublishRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.CompletedTask;
    }
}

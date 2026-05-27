using Microsoft.Extensions.Logging.Abstractions;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;
using NursingCareBackend.Application.CareRequests.Commands.PayCareRequest;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// T2.1 — PayCareRequestHandler auto-generates the client receipt on payment confirmation and
/// notifies the client. The receipt step is FAILURE-ISOLATED: a receipt/PDF error must never fail
/// the payment. In-memory fakes, no SQL.
/// </summary>
public sealed class PayCareRequestHandlerTests
{
    [Fact]
    public async Task Pay_AutoGeneratesReceipt_AndNotifiesReceiptReady()
    {
        var request = BuildInvoicedRequest();
        var repo = new FakePayCrRepo(request);
        var receipt = new RecordingReceiptHandler();
        var notifier = new RecordingPayNotifier();
        var handler = BuildHandler(repo, receipt, notifier);
        var adminId = Guid.NewGuid();

        var result = await handler.Handle(
            new PayCareRequestCommand(request.Id, "TRF-1", DateTime.UtcNow, adminId),
            CancellationToken.None);

        Assert.Equal(CareRequestStatus.Paid, request.Status);
        Assert.Equal(request.Id, result.Id);

        // Receipt auto-generated with the right command (this request + the acting admin).
        Assert.NotNull(receipt.LastCommand);
        Assert.Equal(request.Id, receipt.LastCommand!.CareRequestId);
        Assert.Equal(adminId, receipt.LastCommand.ActingAdminUserId);

        // Client told the receipt is ready.
        Assert.NotNull(notifier.Published);
        Assert.Equal("payment_confirmed", notifier.Published!.Category);
        Assert.Contains("recibo ya está disponible", notifier.Published.Body);
    }

    [Fact]
    public async Task Pay_WhenReceiptGenerationFails_PaymentStillSucceeds_WithoutReceiptClaim()
    {
        var request = BuildInvoicedRequest();
        var repo = new FakePayCrRepo(request);
        var pv = new RecordingPaymentValidationRepo();
        var audit = new RecordingPayAudit();
        var receipt = new ThrowingReceiptHandler();
        var notifier = new RecordingPayNotifier();
        var handler = BuildHandler(repo, receipt, notifier, pv, audit);

        // Must NOT throw — payment is the source of truth.
        var result = await handler.Handle(
            new PayCareRequestCommand(request.Id, "TRF-2", DateTime.UtcNow, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(CareRequestStatus.Paid, request.Status);
        Assert.NotNull(result);

        // Payment side effects still happened.
        Assert.NotNull(pv.Added);
        Assert.NotNull(audit.Record);
        Assert.Equal("Pay", audit.Record!.Action);

        // Client still notified, but WITHOUT promising a receipt that doesn't exist.
        Assert.NotNull(notifier.Published);
        Assert.DoesNotContain("recibo", notifier.Published!.Body);
    }

    [Fact]
    public async Task Pay_NotFound_Throws_AndDoesNotGenerateReceipt()
    {
        var receipt = new RecordingReceiptHandler();
        var handler = BuildHandler(new FakePayCrRepo(null), receipt, new RecordingPayNotifier());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(
                new PayCareRequestCommand(Guid.NewGuid(), "TRF-3", DateTime.UtcNow, Guid.NewGuid()),
                CancellationToken.None));

        Assert.Null(receipt.LastCommand);
    }

    [Fact]
    public async Task Pay_WhenNotificationFails_PaymentStillSucceeds()
    {
        var request = BuildInvoicedRequest();
        var pv = new RecordingPaymentValidationRepo();
        var handler = BuildHandler(
            new FakePayCrRepo(request),
            new RecordingReceiptHandler(),
            new ThrowingPayNotifier(),
            pv);

        // Notification is a soft side-effect — its failure must NOT fail the committed payment.
        var result = await handler.Handle(
            new PayCareRequestCommand(request.Id, "TRF-4", DateTime.UtcNow, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(CareRequestStatus.Paid, request.Status);
        Assert.NotNull(result);
        Assert.NotNull(pv.Added); // payment side effects persisted
    }

    [Fact]
    public async Task Pay_WhenReceiptGenerationCancelled_PropagatesCancellation()
    {
        // Cancellation must NOT be swallowed as a "receipt failed" — it propagates.
        var request = BuildInvoicedRequest();
        var handler = BuildHandler(
            new FakePayCrRepo(request),
            new CancelingReceiptHandler(),
            new RecordingPayNotifier());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.Handle(
                new PayCareRequestCommand(request.Id, "TRF-5", DateTime.UtcNow, Guid.NewGuid()),
                CancellationToken.None));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static PayCareRequestHandler BuildHandler(
        ICareRequestRepository repo,
        IGenerateReceiptHandler receipt,
        IUserNotificationPublisher notifier,
        IPaymentValidationRepository? pv = null,
        IAdminAuditService? audit = null)
        => new(
            repo,
            pv ?? new RecordingPaymentValidationRepo(),
            audit ?? new RecordingPayAudit(),
            notifier,
            receipt,
            NullLogger<PayCareRequestHandler>.Instance);

    private static CareRequest BuildInvoicedRequest()
    {
        var nurse = Guid.NewGuid();
        var request = CareRequest.Create(new CareRequestCreateParams
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
            CareRequestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            PricingCategoryCode = "domicilio",
            CategoryFactorSnapshot = 1.2m,
            DistanceFactorMultiplierSnapshot = 1.0m,
            ComplexityMultiplierSnapshot = 1.0m,
            VolumeDiscountPercentSnapshot = 0,
            LineBeforeVolumeDiscount = null,
            UnitPriceAfterVolumeDiscount = null,
            SubtotalBeforeSupplies = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
        });
        request.Approve(DateTime.UtcNow.AddDays(-2));
        request.Complete(DateTime.UtcNow.AddHours(-12), nurse);
        request.Invoice("FAC-20260101-0001", DateTime.UtcNow.AddHours(-6));
        return request;
    }
}

// ── file-scoped fakes ─────────────────────────────────────────────────────────

file sealed class FakePayCrRepo(CareRequest? careRequest) : ICareRequestRepository
{
    public Task AddAsync(CareRequest cr, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateAsync(CareRequest cr, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<CareRequest>> GetAllAsync(CareRequestAccessScope scope, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CareRequest>>(careRequest is null ? Array.Empty<CareRequest>() : new[] { careRequest });
    public Task<CareRequest?> GetByIdAsync(Guid id, CareRequestAccessScope scope, CancellationToken ct)
        => Task.FromResult(careRequest is not null && careRequest.Id == id ? careRequest : null);
    public Task<int> CountByUserAndUnitTypeAsync(Guid userID, string unitType, CancellationToken ct)
        => Task.FromResult(0);
}

file sealed class RecordingPaymentValidationRepo : IPaymentValidationRepository
{
    public PaymentValidation? Added { get; private set; }
    public Task AddAsync(PaymentValidation pv, CancellationToken ct) { Added = pv; return Task.CompletedTask; }
    public Task<PaymentValidation?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken ct)
        => Task.FromResult<PaymentValidation?>(null);
}

file sealed class RecordingPayAudit : IAdminAuditService
{
    public AdminAuditRecord? Record { get; private set; }
    public Task WriteAsync(AdminAuditRecord record, CancellationToken ct = default) { Record = record; return Task.CompletedTask; }
}

file sealed class RecordingPayNotifier : IUserNotificationPublisher
{
    public UserNotificationPublishRequest? Published { get; private set; }
    public Task PublishToUserAsync(UserNotificationPublishRequest request, CancellationToken ct = default)
    {
        Published = request;
        return Task.CompletedTask;
    }
}

file sealed class RecordingReceiptHandler : IGenerateReceiptHandler
{
    public GenerateReceiptCommand? LastCommand { get; private set; }
    public Task<GenerateReceiptResponse> Handle(GenerateReceiptCommand command, CancellationToken ct)
    {
        LastCommand = command;
        return Task.FromResult(new GenerateReceiptResponse(Guid.NewGuid(), "REC-20260101-0001", "JVBERi0="));
    }
}

file sealed class ThrowingReceiptHandler : IGenerateReceiptHandler
{
    public Task<GenerateReceiptResponse> Handle(GenerateReceiptCommand command, CancellationToken ct)
        => throw new InvalidOperationException("PDF generation failed");
}

file sealed class CancelingReceiptHandler : IGenerateReceiptHandler
{
    public Task<GenerateReceiptResponse> Handle(GenerateReceiptCommand command, CancellationToken ct)
        => throw new OperationCanceledException();
}

file sealed class ThrowingPayNotifier : IUserNotificationPublisher
{
    public Task PublishToUserAsync(UserNotificationPublishRequest request, CancellationToken ct = default)
        => throw new InvalidOperationException("notification transport failed");
}

using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.IssueCreditNote;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Unit tests for IssueCreditNoteHandler. In-memory fakes, no SQL. Covers: persists the credit note,
/// writes the IssueCreditNote audit, notifies the CLIENT, reads the running already-credited total
/// and enforces the cap through the domain, and guards a non-Paid / missing request.
/// </summary>
public sealed class IssueCreditNoteHandlerTests
{
    [Fact]
    public async Task Handle_PersistsCreditNote_Audits_And_NotifiesClient()
    {
        var request = BuildPaidCareRequest(total: 4200m);
        var repo = new FakeCrRepo(request);
        var notes = new FakeCreditNoteRepo(alreadyCredited: 0m);
        var audit = new RecordingAuditService();
        var notifier = new RecordingNotifier();
        var handler = new IssueCreditNoteHandler(repo, notes, audit, notifier);
        var adminId = Guid.NewGuid();

        var result = await handler.Handle(
            new IssueCreditNoteCommand(request.Id, 1500m, "Reembolso", "TRF-77", adminId),
            CancellationToken.None);

        // Persisted
        Assert.NotNull(notes.Added);
        Assert.Equal(1500m, notes.Added!.Amount);
        Assert.Equal("Reembolso", notes.Added.Reason);
        Assert.Equal("TRF-77", notes.Added.Reference);
        Assert.Equal(adminId, notes.Added.IssuedByUserId);

        // Response carries running total
        Assert.Equal(1500m, result.Amount);
        Assert.Equal(1500m, result.TotalCredited);

        // Audited with the right action against the CareRequest
        Assert.NotNull(audit.Record);
        Assert.Equal(AdminAuditActions.IssueCreditNote, audit.Record!.Action);
        Assert.Equal("CareRequest", audit.Record.EntityType);
        Assert.Equal(request.Id.ToString(), audit.Record.EntityId);

        // Client (request owner) notified
        Assert.NotNull(notifier.Published);
        Assert.Equal(request.UserID, notifier.Published!.RecipientUserId);
        Assert.Equal("credit_note_issued", notifier.Published.Category);
    }

    [Fact]
    public async Task Handle_AddsToAlreadyCreditedTotal_InResponse()
    {
        var request = BuildPaidCareRequest(total: 4200m);
        var notes = new FakeCreditNoteRepo(alreadyCredited: 1000m);
        var handler = new IssueCreditNoteHandler(
            new FakeCrRepo(request), notes, new RecordingAuditService(), new RecordingNotifier());

        var result = await handler.Handle(
            new IssueCreditNoteCommand(request.Id, 500m, "Otro", null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(1500m, result.TotalCredited); // 1000 prior + 500 new
    }

    [Fact]
    public async Task Handle_WhenCumulativeExceedsPaid_Throws_And_PersistsNothing()
    {
        var request = BuildPaidCareRequest(total: 4200m);
        var notes = new FakeCreditNoteRepo(alreadyCredited: 4000m); // only 200 headroom
        var handler = new IssueCreditNoteHandler(
            new FakeCrRepo(request), notes, new RecordingAuditService(), new RecordingNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new IssueCreditNoteCommand(request.Id, 500m, "Excede", null, Guid.NewGuid()),
                CancellationToken.None));

        Assert.Null(notes.Added); // nothing persisted when the cap is breached
    }

    [Fact]
    public async Task Handle_WhenRequestNotPaid_Throws()
    {
        var request = BuildCompletedCareRequest(); // not Paid
        var handler = new IssueCreditNoteHandler(
            new FakeCrRepo(request), new FakeCreditNoteRepo(0m),
            new RecordingAuditService(), new RecordingNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new IssueCreditNoteCommand(request.Id, 100m, "Motivo", null, Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenRequestNotFound_Throws()
    {
        var handler = new IssueCreditNoteHandler(
            new FakeCrRepo(null), new FakeCreditNoteRepo(0m),
            new RecordingAuditService(), new RecordingNotifier());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(
                new IssueCreditNoteCommand(Guid.NewGuid(), 100m, "Motivo", null, Guid.NewGuid()),
                CancellationToken.None));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CareRequest BuildCompletedCareRequest(decimal total = 4200m)
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
            Total = total,
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
        return request;
    }

    private static CareRequest BuildPaidCareRequest(decimal total = 4200m)
    {
        var request = BuildCompletedCareRequest(total);
        request.Invoice("FAC-20260101-0001", DateTime.UtcNow.AddHours(-6));
        request.Pay("REF-BANCO-001", DateTime.UtcNow.AddHours(-1));
        return request;
    }
}

// ── file-scoped fakes ─────────────────────────────────────────────────────────

file sealed class FakeCrRepo(CareRequest? careRequest) : ICareRequestRepository
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

file sealed class FakeCreditNoteRepo(decimal alreadyCredited) : ICreditNoteRepository
{
    public CreditNote? Added { get; private set; }

    public Task<decimal> GetTotalCreditedAsync(Guid careRequestId, CancellationToken ct)
        => Task.FromResult(alreadyCredited);
    public Task<IReadOnlyList<CreditNote>> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CreditNote>>(Array.Empty<CreditNote>());
    public Task AddAsync(CreditNote creditNote, CancellationToken ct)
    {
        Added = creditNote;
        return Task.CompletedTask;
    }
}

file sealed class RecordingAuditService : IAdminAuditService
{
    public AdminAuditRecord? Record { get; private set; }
    public Task WriteAsync(AdminAuditRecord record, CancellationToken ct = default)
    {
        Record = record;
        return Task.CompletedTask;
    }
}

file sealed class RecordingNotifier : IUserNotificationPublisher
{
    public UserNotificationPublishRequest? Published { get; private set; }
    public Task PublishToUserAsync(UserNotificationPublishRequest request, CancellationToken ct = default)
    {
        Published = request;
        return Task.CompletedTask;
    }
}

using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Queries.GetPaymentClaim;
using NursingCareBackend.Domain.CareRequests;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>Anti-fraud review: GetPaymentClaimHandler surfaces the structured claim and the
/// amount-mismatch / reused-reference flags for the admin. In-memory fakes, no SQL.</summary>
public sealed class GetPaymentClaimHandlerTests
{
    [Fact]
    public async Task Handle_RequestNotFound_ReturnsNull()
    {
        var handler = new GetPaymentClaimHandler(
            new ClaimCrRepo(null), new ClaimProofRepo(null), new ClaimValidationRepo(false));

        Assert.Null(await handler.Handle(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoProof_ReturnsHasProofFalse_WithInvoiceTotal()
    {
        var request = BuildPaidRequest();
        var handler = new GetPaymentClaimHandler(
            new ClaimCrRepo(request), new ClaimProofRepo(null), new ClaimValidationRepo(false));

        var result = await handler.Handle(request.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.HasProof);
        Assert.Equal(4200m, result.InvoiceTotal);
        Assert.False(result.AmountReported);
        Assert.False(result.ReusedReference);
    }

    [Fact]
    public async Task Handle_AmountMatches_True_NoMismatch()
    {
        var request = BuildPaidRequest();
        var proof = Proof(request.Id, amount: 4200m, reference: "TRF-OK");
        var handler = new GetPaymentClaimHandler(
            new ClaimCrRepo(request), new ClaimProofRepo(proof), new ClaimValidationRepo(false));

        var result = await handler.Handle(request.Id, CancellationToken.None);

        Assert.True(result!.HasProof);
        Assert.True(result.AmountReported);
        Assert.True(result.AmountMatches);
        Assert.Equal("TRF-OK", result.ClaimedBankReference);
    }

    [Fact]
    public async Task Handle_AmountMismatch_MatchesFalse()
    {
        var request = BuildPaidRequest();
        var proof = Proof(request.Id, amount: 4000m, reference: "TRF-X");
        var handler = new GetPaymentClaimHandler(
            new ClaimCrRepo(request), new ClaimProofRepo(proof), new ClaimValidationRepo(false));

        var result = await handler.Handle(request.Id, CancellationToken.None);

        Assert.True(result!.AmountReported);
        Assert.False(result.AmountMatches);
    }

    [Fact]
    public async Task Handle_ReusedReference_TrueWhenValidationSaysUsed()
    {
        var request = BuildPaidRequest();
        var proof = Proof(request.Id, amount: 4200m, reference: "TRF-DUP");
        var handler = new GetPaymentClaimHandler(
            new ClaimCrRepo(request), new ClaimProofRepo(proof), new ClaimValidationRepo(true));

        var result = await handler.Handle(request.Id, CancellationToken.None);

        Assert.True(result!.ReusedReference);
    }

    [Fact]
    public async Task Handle_ReusedReference_FalseWhenNoReferenceClaimed()
    {
        var request = BuildPaidRequest();
        var proof = Proof(request.Id, amount: 4200m, reference: null);
        // Even if the repo would say "used", a blank claimed reference can't be a reuse.
        var handler = new GetPaymentClaimHandler(
            new ClaimCrRepo(request), new ClaimProofRepo(proof), new ClaimValidationRepo(true));

        var result = await handler.Handle(request.Id, CancellationToken.None);

        Assert.False(result!.ReusedReference);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static PaymentProof Proof(Guid careRequestId, decimal? amount, string? reference)
        => PaymentProof.Create(
            careRequestId, new byte[] { 1 }, "image/png", null, Guid.NewGuid(), DateTime.UtcNow,
            claimedBankReference: reference, claimedAmount: amount,
            claimedPaymentDate: new DateOnly(2026, 5, 26), payingBank: "Banreservas");

    private static CareRequest BuildPaidRequest()
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
        request.Invoice("FAC-1", DateTime.UtcNow.AddHours(-6));
        request.Pay("TRF-OK", DateTime.UtcNow.AddHours(-1));
        return request;
    }
}

// ── file-scoped fakes ─────────────────────────────────────────────────────────

file sealed class ClaimCrRepo(CareRequest? careRequest) : ICareRequestRepository
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

file sealed class ClaimProofRepo(PaymentProof? proof) : IPaymentProofRepository
{
    public Task AddAsync(PaymentProof p, CancellationToken ct) => Task.CompletedTask;
    public Task<PaymentProof?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(proof);
    public Task<PaymentProof?> GetLatestByCareRequestIdAsync(Guid careRequestId, CancellationToken ct)
        => Task.FromResult(proof);
}

file sealed class ClaimValidationRepo(bool referenceUsed) : IPaymentValidationRepository
{
    public Task AddAsync(PaymentValidation pv, CancellationToken ct) => Task.CompletedTask;
    public Task<PaymentValidation?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken ct)
        => Task.FromResult<PaymentValidation?>(null);
    public Task<bool> IsBankReferenceUsedAsync(string bankReference, Guid excludeCareRequestId, CancellationToken ct)
        => Task.FromResult(referenceUsed);
}

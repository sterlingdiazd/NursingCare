using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;
using NursingCareBackend.Application.Identity.Models;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using Xunit;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Unit tests for GenerateReceiptHandler. Uses in-memory fakes so no SQL Server is required.
/// Covers: company name propagation from ICompanyInfoProvider, idempotent re-generation,
/// and guard against non-Paid care requests.
/// </summary>
public sealed class GenerateReceiptHandlerTests
{
    private const string TestCompanyName = "Sol y Luna Test";

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CareRequest BuildPaidCareRequest()
    {
        var request = CareRequest.Create(new CareRequestCreateParams
        {
            UserID = Guid.NewGuid(),
            Description = "Test care request",
            CareRequestReason = "Test reason",
            CareRequestType = "hogar_diario",
            UnitType = "dia_completo",
            SuggestedNurse = null,
            AssignedNurse = Guid.NewGuid(),
            Unit = 3,
            Price = 2500m,
            Total = 7500m,
            ClientBasePrice = null,
            DistanceFactor = "local",
            ComplexityLevel = "estandar",
            MedicalSuppliesCost = null,
            // Scheduled 2 days ago so completion (12h ago) is never before the care-request date,
            // regardless of the current time of day (the domain forbids completing before the date).
            CareRequestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            PricingCategoryCode = "hogar",
            CategoryFactorSnapshot = 1.0m,
            DistanceFactorMultiplierSnapshot = 1.0m,
            ComplexityMultiplierSnapshot = 1.0m,
            VolumeDiscountPercentSnapshot = 0,
            LineBeforeVolumeDiscount = null,
            UnitPriceAfterVolumeDiscount = null,
            SubtotalBeforeSupplies = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
        });

        request.Approve(DateTime.UtcNow.AddDays(-2));
        request.Complete(DateTime.UtcNow.AddHours(-12), request.AssignedNurse!.Value);
        request.Invoice("FAC-20260101-0001", DateTime.UtcNow.AddHours(-6));
        request.Pay("REF-BANCO-001", DateTime.UtcNow.AddHours(-1));
        return request;
    }

    private static GenerateReceiptHandler BuildHandlerForStatusTest(CareRequest careRequest)
    {
        return new GenerateReceiptHandler(
            repository: new FakeCareRequestRepository(careRequest),
            receiptRepository: new FakeReceiptRepository(),
            paymentValidationRepository: new FakePaymentValidationRepository(),
            receiptPdfService: new FakeReceiptPdfService(_ => { }),
            userRepository: new FakeUserRepository(),
            auditService: new FakeAuditService(),
            companyInfoProvider: new FakeCompanyInfoProvider(new CompanyInfo(TestCompanyName, null, null, null)));
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PopulatesCompanyNameFromProvider_InPdfData()
    {
        // Arrange
        var careRequest = BuildPaidCareRequest();
        ReceiptPdfData? captured = null;
        var pdfService = new FakeReceiptPdfService(d => captured = d);
        var handler = new GenerateReceiptHandler(
            repository: new FakeCareRequestRepository(careRequest),
            receiptRepository: new FakeReceiptRepository(),
            paymentValidationRepository: new FakePaymentValidationRepository(),
            receiptPdfService: pdfService,
            userRepository: new FakeUserRepository(),
            auditService: new FakeAuditService(),
            companyInfoProvider: new FakeCompanyInfoProvider(new CompanyInfo(TestCompanyName, null, null, null)));

        var command = new GenerateReceiptCommand(careRequest.Id, Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(captured);
        Assert.Equal(TestCompanyName, captured!.CompanyName);
    }

    [Fact]
    public async Task Handle_PopulatesClientIdentificationNumber_FromUser()
    {
        // T2.3: the client's cédula/RNC (User.IdentificationNumber) must reach the receipt PDF data,
        // instead of the previous hardcoded null.
        var careRequest = BuildPaidCareRequest();
        ReceiptPdfData? captured = null;
        var pdfService = new FakeReceiptPdfService(d => captured = d);
        var handler = new GenerateReceiptHandler(
            repository: new FakeCareRequestRepository(careRequest),
            receiptRepository: new FakeReceiptRepository(),
            paymentValidationRepository: new FakePaymentValidationRepository(),
            receiptPdfService: pdfService,
            userRepository: new FakeUserRepositoryWithId("00112345678"),
            auditService: new FakeAuditService(),
            companyInfoProvider: new FakeCompanyInfoProvider(new CompanyInfo(TestCompanyName, null, null, null)));

        await handler.Handle(new GenerateReceiptCommand(careRequest.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("00112345678", captured!.ClientIdentificationNumber);
    }

    [Fact]
    public async Task Handle_ReturnsExistingReceipt_WhenAlreadyGenerated()
    {
        // Arrange
        var careRequest = BuildPaidCareRequest();
        var existingReceipt = Receipt.Create(
            careRequestId: careRequest.Id,
            receiptNumber: "REC-20260101-0001",
            receiptContent: new byte[] { 1, 2, 3 },
            generatedByUserId: Guid.NewGuid(),
            generatedAtUtc: DateTime.UtcNow.AddMinutes(-10));

        var pdfService = new FakeReceiptPdfService(_ => throw new InvalidOperationException("PDF should not be regenerated"));
        var handler = new GenerateReceiptHandler(
            repository: new FakeCareRequestRepository(careRequest),
            receiptRepository: new FakeReceiptRepository(existingReceipt),
            paymentValidationRepository: new FakePaymentValidationRepository(),
            receiptPdfService: pdfService,
            userRepository: new FakeUserRepository(),
            auditService: new FakeAuditService(),
            companyInfoProvider: new FakeCompanyInfoProvider(new CompanyInfo(TestCompanyName, null, null, null)));

        var command = new GenerateReceiptCommand(careRequest.Id, Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — existing receipt returned, PDF not regenerated
        Assert.Equal(existingReceipt.Id, result.ReceiptId);
        Assert.Equal("REC-20260101-0001", result.ReceiptNumber);
    }

    [Fact]
    public async Task Handle_Throws_WhenCareRequestNotPaid()
    {
        // Arrange — care request is Approved (not Paid)
        var request = CareRequest.Create(new CareRequestCreateParams
        {
            UserID = Guid.NewGuid(),
            Description = "Test",
            CareRequestReason = "Reason",
            CareRequestType = "hogar_diario",
            UnitType = "dia_completo",
            SuggestedNurse = null,
            AssignedNurse = Guid.NewGuid(),
            Unit = 1,
            Price = 2500m,
            Total = 2500m,
            ClientBasePrice = null,
            DistanceFactor = "local",
            ComplexityLevel = "estandar",
            MedicalSuppliesCost = null,
            CareRequestDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PricingCategoryCode = "hogar",
            CategoryFactorSnapshot = 1.0m,
            DistanceFactorMultiplierSnapshot = 1.0m,
            ComplexityMultiplierSnapshot = 1.0m,
            VolumeDiscountPercentSnapshot = 0,
            LineBeforeVolumeDiscount = null,
            UnitPriceAfterVolumeDiscount = null,
            SubtotalBeforeSupplies = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        request.Approve(DateTime.UtcNow.AddDays(-1));

        var handler = BuildHandlerForStatusTest(request);
        var command = new GenerateReceiptCommand(request.Id, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}

// ── file-scoped fakes ─────────────────────────────────────────────────────────

file sealed class FakeCareRequestRepository(CareRequest careRequest) : ICareRequestRepository
{
    public Task AddAsync(CareRequest cr, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateAsync(CareRequest cr, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<CareRequest>> GetAllAsync(CareRequestAccessScope scope, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CareRequest>>(new[] { careRequest });
    public Task<CareRequest?> GetByIdAsync(Guid id, CareRequestAccessScope scope, CancellationToken ct)
        => Task.FromResult<CareRequest?>(careRequest.Id == id ? careRequest : null);
    public Task<int> CountByUserAndUnitTypeAsync(Guid userID, string unitType, CancellationToken ct)
        => Task.FromResult(0);
}

file sealed class FakeReceiptRepository(Receipt? existing = null) : IReceiptRepository
{
    public Task<Receipt?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken ct)
        => Task.FromResult(existing);
    public Task AddAsync(Receipt receipt, CancellationToken ct) => Task.CompletedTask;
    public Task<int> CountByDateAsync(DateOnly date, CancellationToken ct) => Task.FromResult(0);
}

file sealed class FakePaymentValidationRepository : IPaymentValidationRepository
{
    public Task AddAsync(PaymentValidation pv, CancellationToken ct) => Task.CompletedTask;
    public Task<PaymentValidation?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken ct)
        => Task.FromResult<PaymentValidation?>(null);
    public Task<bool> IsBankReferenceUsedAsync(string bankReference, Guid excludeCareRequestId, CancellationToken ct)
        => Task.FromResult(false);
}

file sealed class FakeReceiptPdfService(Action<ReceiptPdfData> onGenerate) : IReceiptPdfService
{
    public byte[] Generate(ReceiptPdfData data)
    {
        onGenerate(data);
        return new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    }
}

file sealed class FakeUserRepository : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<User?>(null);
    public Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    public Task<bool> AnyAdminExistsAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<IReadOnlyList<User>> GetNurseProfilesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    public Task<IReadOnlyList<User>> GetPendingNurseProfilesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    public Task<IReadOnlyList<User>> GetActiveNurseProfilesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    public Task<IReadOnlyDictionary<Guid, NurseWorkloadSummary>> GetNurseWorkloadsAsync(IReadOnlyCollection<Guid> nurseUserIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, NurseWorkloadSummary>>(new Dictionary<Guid, NurseWorkloadSummary>());
    public Task<bool> HasAssignedCareRequestsAsync(Guid nurseUserId, CancellationToken ct = default) => Task.FromResult(false);
    public Task<User> CreateAsync(User user, CancellationToken ct = default) => Task.FromResult(user);
    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class FakeAuditService : IAdminAuditService
{
    public Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class FakeUserRepositoryWithId(string identificationNumber) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<User?>(new User
        {
            Id = userId,
            Email = "cliente@example.com",
            Name = "Cliente",
            LastName = "Prueba",
            IdentificationNumber = identificationNumber,
        });
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<User?>(null);
    public Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    public Task<bool> AnyAdminExistsAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<IReadOnlyList<User>> GetNurseProfilesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    public Task<IReadOnlyList<User>> GetPendingNurseProfilesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    public Task<IReadOnlyList<User>> GetActiveNurseProfilesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    public Task<IReadOnlyDictionary<Guid, NurseWorkloadSummary>> GetNurseWorkloadsAsync(IReadOnlyCollection<Guid> nurseUserIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, NurseWorkloadSummary>>(new Dictionary<Guid, NurseWorkloadSummary>());
    public Task<bool> HasAssignedCareRequestsAsync(Guid nurseUserId, CancellationToken ct = default) => Task.FromResult(false);
    public Task<User> CreateAsync(User user, CancellationToken ct = default) => Task.FromResult(user);
    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class FakeCompanyInfoProvider(CompanyInfo info) : ICompanyInfoProvider
{
    public Task<CompanyInfo> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(info);
}

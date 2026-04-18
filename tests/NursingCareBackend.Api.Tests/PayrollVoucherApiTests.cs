using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

/// <summary>
/// Integration tests for payroll voucher endpoints (PDF individual, bulk ZIP, nurse self-service).
/// Authored by code-review-qa-agent for initiative 2026-04-17T2100-payroll-pdf-vouchers.
/// </summary>
public sealed class PayrollVoucherApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PayrollVoucherApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC: Admin individual voucher endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminVoucher_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/voucher/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminVoucher_With_NurseToken_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/voucher/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminVoucher_Missing_Period_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/voucher/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminVoucher_Valid_Returns_Pdf_ContentType()
    {
        var (periodId, nurseId) = await SeedVoucherDataAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/voucher/{nurseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminVoucher_Returns_ContentDisposition_Header()
    {
        var (periodId, nurseId) = await SeedVoucherDataAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/voucher/{nurseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.True(
            response.Content.Headers.ContentDisposition.FileName?.EndsWith(".pdf") == true
            || response.Content.Headers.ContentDisposition.FileNameStar?.EndsWith(".pdf") == true,
            "Content-Disposition filename should end with .pdf");
    }

    [Fact]
    public async Task AdminVoucher_Returns_Valid_Pdf_MagicBytes()
    {
        var (periodId, nurseId) = await SeedVoucherDataAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/voucher/{nurseId}");

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100, "PDF should be non-trivially sized");
        // %PDF magic bytes
        Assert.Equal(0x25, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
        Assert.Equal(0x46, bytes[3]);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC: Admin bulk ZIP endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BulkZip_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/vouchers/zip");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BulkZip_With_NurseToken_Returns_403()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/vouchers/zip");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BulkZip_Missing_Period_Returns_404()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{Guid.NewGuid()}/vouchers/zip");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BulkZip_Valid_Returns_Zip_ContentType()
    {
        var (periodId, _) = await SeedVoucherDataAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/vouchers/zip");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BulkZip_Contains_Pdf_Entries()
    {
        var (periodId, _) = await SeedVoucherDataAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/vouchers/zip");

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var zipStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        Assert.True(archive.Entries.Count >= 1, "ZIP should contain at least one entry");
        foreach (var entry in archive.Entries)
        {
            Assert.EndsWith(".pdf", entry.Name);
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);
            var entryBytes = ms.ToArray();
            Assert.True(entryBytes.Length > 100, $"PDF entry '{entry.Name}' should be non-trivially sized");
            // Verify PDF magic bytes
            Assert.Equal(0x25, entryBytes[0]);
            Assert.Equal(0x50, entryBytes[1]);
        }
    }

    [Fact]
    public async Task BulkZip_Returns_ContentDisposition_Header()
    {
        var (periodId, _) = await SeedVoucherDataAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/vouchers/zip");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.True(
            response.Content.Headers.ContentDisposition.FileName?.EndsWith(".zip") == true
            || response.Content.Headers.ContentDisposition.FileNameStar?.EndsWith(".zip") == true,
            "Content-Disposition filename should end with .zip");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RBAC: Nurse self-service voucher endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NurseVoucher_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/nurse/payroll/periods/{Guid.NewGuid()}/voucher");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NurseVoucher_With_AdminToken_Returns_403()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/nurse/payroll/periods/{Guid.NewGuid()}/voucher");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NurseVoucher_Missing_Period_Returns_404()
    {
        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/nurse/payroll/periods/{Guid.NewGuid()}/voucher");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NurseVoucher_Valid_Returns_Pdf_ContentType()
    {
        var (periodId, _) = await SeedVoucherDataForTestNurseAsync();

        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/nurse/payroll/periods/{periodId}/voucher");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NurseVoucher_Returns_Valid_Pdf_MagicBytes()
    {
        var (periodId, _) = await SeedVoucherDataForTestNurseAsync();

        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/nurse/payroll/periods/{periodId}/voucher");

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.Equal(0x25, bytes[0]); // %PDF
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
        Assert.Equal(0x46, bytes[3]);
    }

    [Fact]
    public async Task NurseVoucher_Returns_ContentDisposition_Header()
    {
        var (periodId, _) = await SeedVoucherDataForTestNurseAsync();

        var client = CreateNurseClient();
        var response = await client.GetAsync(
            $"/api/nurse/payroll/periods/{periodId}/voucher");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.True(
            response.Content.Headers.ContentDisposition.FileName?.Contains("comprobante") == true
            || response.Content.Headers.ContentDisposition.FileNameStar?.Contains("comprobante") == true,
            "Nurse voucher filename should contain 'comprobante'");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Security: Nurse cannot access other nurse's data via admin endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NurseVoucher_Only_Returns_Own_Data()
    {
        // The nurse endpoint extracts nurse ID from JWT claims, not URL params.
        // A nurse with no data for a period gets 404, not someone else's PDF.
        var client = CreateNurseClient();
        var periodId = await SeedEmptyPeriodAsync();

        var response = await client.GetAsync(
            $"/api/nurse/payroll/periods/{periodId}/voucher");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Boundary: Non-GUID path segments and empty periods
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminVoucher_Invalid_Guid_Returns_404_Or_BadRequest()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync(
            "/api/admin/payroll/periods/not-a-guid/voucher/also-not-a-guid");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BulkZip_Empty_Period_Returns_404()
    {
        var periodId = await SeedEmptyPeriodAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/vouchers/zip");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminVoucher_Period_Exists_But_Nurse_Not_In_Period_Returns_404()
    {
        var periodId = await SeedEmptyPeriodAsync();

        var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/api/admin/payroll/periods/{periodId}/voucher/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Data seeding helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private static int _voucherPeriodCounter = 100;

    private async Task<(Guid PeriodId, Guid NurseId)> SeedVoucherDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var offset = System.Threading.Interlocked.Increment(ref _voucherPeriodCounter);
        var start = new DateOnly(2097, 1, 1).AddDays(offset * 30);
        var end = start.AddDays(14);

        var period = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
        db.PayrollPeriods.Add(period);

        // Create a minimal user record for voucher display name resolution
        var user = new NursingCareBackend.Domain.Identity.User
        {
            Id = Guid.NewGuid(),
            Email = $"voucher-test-{offset}@test.local",
            Name = "Test",
            LastName = "VoucherNurse",
            PasswordHash = "not-a-real-hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        var nurseId = user.Id;

        var exec = ServiceExecution.Create(
            careRequestId: Guid.NewGuid(),
            nurseUserId: nurseId,
            shiftRecordId: null,
            compensationRuleId: null,
            employmentType: CompensationEmploymentType.PerService,
            variant: ServiceExecutionVariant.Standard,
            executedAtUtc: DateTime.UtcNow,
            careRequestType: "TestType",
            unitType: "hour",
            unit: 1,
            pricingCategoryCode: null,
            distanceFactorCode: null,
            complexityLevelCode: null,
            basePrice: 100m,
            careRequestTotal: 100m,
            clientBasePrice: 100m,
            categoryFactorSnapshot: 1m,
            distanceMultiplierSnapshot: 1m,
            complexityMultiplierSnapshot: 1m,
            volumeDiscountPercentSnapshot: 0,
            subtotalBeforeSupplies: 100m,
            medicalSuppliesCost: 0m,
            ruleBaseCompensationPercent: 55m,
            ruleFixedAmountPerUnit: 0m,
            ruleTransportIncentivePercent: 10m,
            ruleComplexityBonusPercent: 15m,
            ruleMedicalSuppliesPercent: 0m,
            ruleVariantPercent: 100m,
            baseCompensation: 55m,
            transportIncentive: 10m,
            complexityBonus: 5m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            manualOverrideAmount: null,
            notes: null,
            createdAtUtc: DateTime.UtcNow);

        db.ServiceExecutions.Add(exec);
        await db.SaveChangesAsync();

        var line = PayrollLine.Create(
            payrollPeriodId: period.Id,
            nurseUserId: nurseId,
            serviceExecutionId: exec.Id,
            description: "Voucher test line",
            baseCompensation: 55m,
            transportIncentive: 10m,
            complexityBonus: 5m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            createdAtUtc: DateTime.UtcNow);

        db.PayrollLines.Add(line);
        await db.SaveChangesAsync();

        return (period.Id, nurseId);
    }

    private async Task<(Guid PeriodId, Guid NurseId)> SeedVoucherDataForTestNurseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var nurseId = JwtTestTokens.TestNurseUserId;
        var offset = System.Threading.Interlocked.Increment(ref _voucherPeriodCounter);
        var start = new DateOnly(2097, 1, 1).AddDays(offset * 30);
        var end = start.AddDays(14);

        var period = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
        db.PayrollPeriods.Add(period);

        var exec = ServiceExecution.Create(
            careRequestId: Guid.NewGuid(),
            nurseUserId: nurseId,
            shiftRecordId: null,
            compensationRuleId: null,
            employmentType: CompensationEmploymentType.PerService,
            variant: ServiceExecutionVariant.Standard,
            executedAtUtc: DateTime.UtcNow,
            careRequestType: "TestType",
            unitType: "hour",
            unit: 1,
            pricingCategoryCode: null,
            distanceFactorCode: null,
            complexityLevelCode: null,
            basePrice: 100m,
            careRequestTotal: 100m,
            clientBasePrice: 100m,
            categoryFactorSnapshot: 1m,
            distanceMultiplierSnapshot: 1m,
            complexityMultiplierSnapshot: 1m,
            volumeDiscountPercentSnapshot: 0,
            subtotalBeforeSupplies: 100m,
            medicalSuppliesCost: 0m,
            ruleBaseCompensationPercent: 55m,
            ruleFixedAmountPerUnit: 0m,
            ruleTransportIncentivePercent: 10m,
            ruleComplexityBonusPercent: 15m,
            ruleMedicalSuppliesPercent: 0m,
            ruleVariantPercent: 100m,
            baseCompensation: 55m,
            transportIncentive: 10m,
            complexityBonus: 5m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            manualOverrideAmount: null,
            notes: null,
            createdAtUtc: DateTime.UtcNow);

        db.ServiceExecutions.Add(exec);
        await db.SaveChangesAsync();

        var line = PayrollLine.Create(
            payrollPeriodId: period.Id,
            nurseUserId: nurseId,
            serviceExecutionId: exec.Id,
            description: "Nurse voucher test line",
            baseCompensation: 55m,
            transportIncentive: 10m,
            complexityBonus: 5m,
            medicalSuppliesCompensation: 0m,
            adjustmentsTotal: 0m,
            deductionsTotal: 0m,
            createdAtUtc: DateTime.UtcNow);

        db.PayrollLines.Add(line);
        await db.SaveChangesAsync();

        return (period.Id, nurseId);
    }

    private async Task<Guid> SeedEmptyPeriodAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

        var offset = System.Threading.Interlocked.Increment(ref _voucherPeriodCounter);
        var start = new DateOnly(2097, 1, 1).AddDays(offset * 30);
        var end = start.AddDays(14);

        var period = PayrollPeriod.Create(start, end, end.AddDays(-2), end, DateTime.UtcNow);
        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync();

        return period.Id;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateAdminToken(_factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateNurseClient()
    {
        var client = _factory.CreateClient();
        var token = JwtTestTokens.CreateNurseToken(_factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

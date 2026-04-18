using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Infrastructure.Payroll;
using QuestPDF.Infrastructure;

namespace NursingCareBackend.Application.Tests;

public sealed class PayrollVoucherServiceTests
{
    static PayrollVoucherServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static CompanyInfoOptions DefaultCompanyInfo => new()
    {
        Name = "NursingCare Test",
        Rnc = "123-45678-9",
    };

    private static PayrollVoucherData BuildSampleVoucherData() => new()
    {
        PeriodId = Guid.NewGuid(),
        PeriodStartDate = new DateOnly(2026, 4, 1),
        PeriodEndDate = new DateOnly(2026, 4, 30),
        PaymentDate = new DateOnly(2026, 5, 5),
        PeriodStatus = "Closed",
        NurseUserId = Guid.NewGuid(),
        NurseDisplayName = "Ana Garcia",
        NurseCedula = "001-1234567-8",
        Lines =
        [
            new VoucherLineItem
            {
                Description = "Servicio domicilio 24h",
                BaseCompensation = 3000m,
                TransportIncentive = 200m,
                ComplexityBonus = 150m,
                MedicalSuppliesCompensation = 50m,
                AdjustmentsTotal = 0m,
                DeductionsTotal = 500m,
                NetCompensation = 2900m,
            },
        ],
        Deductions =
        [
            new VoucherDeductionItem
            {
                Label = "Prestamo enero",
                DeductionTypeLabel = "Prestamo",
                Amount = 500m,
            },
        ],
        TotalGross = 3400m,
        TotalTransport = 200m,
        TotalComplexity = 150m,
        TotalSupplies = 50m,
        TotalAdjustments = 0m,
        TotalDeductions = 500m,
        NetCompensation = 2900m,
    };

    [Fact]
    public async Task GenerateVoucherAsync_WithValidData_ReturnsNonEmptyPdfBytes()
    {
        var voucherData = BuildSampleVoucherData();
        var repoMock = new FakeVoucherRepository(voucherData);
        var service = new PayrollVoucherService(repoMock, Options.Create(DefaultCompanyInfo));

        var pdfBytes = await service.GenerateVoucherAsync(voucherData.PeriodId, voucherData.NurseUserId);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 100, "PDF should be non-trivially sized");
        // Verify PDF magic bytes (%PDF)
        Assert.Equal(0x25, pdfBytes[0]); // %
        Assert.Equal(0x50, pdfBytes[1]); // P
        Assert.Equal(0x44, pdfBytes[2]); // D
        Assert.Equal(0x46, pdfBytes[3]); // F
    }

    [Fact]
    public async Task GenerateVoucherAsync_WhenNurseNotFound_ThrowsInvalidOperationException()
    {
        var repoMock = new FakeVoucherRepository(null);
        var service = new PayrollVoucherService(repoMock, Options.Create(DefaultCompanyInfo));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateVoucherAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GenerateBulkVouchersZipAsync_WithMultipleNurses_ReturnsZipBytes()
    {
        var voucherData1 = BuildSampleVoucherData();
        var voucherData2 = new PayrollVoucherData
        {
            PeriodId = voucherData1.PeriodId,
            PeriodStartDate = voucherData1.PeriodStartDate,
            PeriodEndDate = voucherData1.PeriodEndDate,
            PaymentDate = voucherData1.PaymentDate,
            PeriodStatus = voucherData1.PeriodStatus,
            NurseUserId = Guid.NewGuid(),
            NurseDisplayName = "Maria Lopez",
            NurseCedula = null,
            Lines = voucherData1.Lines,
            Deductions = [],
            TotalGross = voucherData1.TotalGross,
            TotalTransport = voucherData1.TotalTransport,
            TotalComplexity = voucherData1.TotalComplexity,
            TotalSupplies = voucherData1.TotalSupplies,
            TotalAdjustments = voucherData1.TotalAdjustments,
            TotalDeductions = voucherData1.TotalDeductions,
            NetCompensation = voucherData1.NetCompensation,
        };

        var repoMock = new FakeVoucherRepository(voucherData1, [voucherData1, voucherData2]);
        var service = new PayrollVoucherService(repoMock, Options.Create(DefaultCompanyInfo));

        var zipBytes = await service.GenerateBulkVouchersZipAsync(voucherData1.PeriodId);

        Assert.NotNull(zipBytes);
        Assert.True(zipBytes.Length > 0);
        // Verify ZIP magic bytes (PK)
        Assert.Equal(0x50, zipBytes[0]); // P
        Assert.Equal(0x4B, zipBytes[1]); // K
    }

    [Fact]
    public async Task GenerateBulkVouchersZipAsync_WhenNoPeriodData_ThrowsInvalidOperationException()
    {
        var repoMock = new FakeVoucherRepository(null, []);
        var service = new PayrollVoucherService(repoMock, Options.Create(DefaultCompanyInfo));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateBulkVouchersZipAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GenerateVoucherAsync_WithNoCedula_ProducesValidPdf()
    {
        var template = BuildSampleVoucherData();
        var voucherData = new PayrollVoucherData
        {
            PeriodId = template.PeriodId,
            PeriodStartDate = template.PeriodStartDate,
            PeriodEndDate = template.PeriodEndDate,
            PaymentDate = template.PaymentDate,
            PeriodStatus = template.PeriodStatus,
            NurseUserId = template.NurseUserId,
            NurseDisplayName = template.NurseDisplayName,
            NurseCedula = null,
            Lines = template.Lines,
            Deductions = template.Deductions,
            TotalGross = template.TotalGross,
            TotalTransport = template.TotalTransport,
            TotalComplexity = template.TotalComplexity,
            TotalSupplies = template.TotalSupplies,
            TotalAdjustments = template.TotalAdjustments,
            TotalDeductions = template.TotalDeductions,
            NetCompensation = template.NetCompensation,
        };
        var repoMock = new FakeVoucherRepository(voucherData);
        var service = new PayrollVoucherService(repoMock, Options.Create(DefaultCompanyInfo));

        var pdfBytes = await service.GenerateVoucherAsync(voucherData.PeriodId, voucherData.NurseUserId);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 100);
    }
}

/// <summary>
/// Fake repository implementation for unit testing the voucher service in isolation.
/// </summary>
file sealed class FakeVoucherRepository : IAdminPayrollRepository
{
    private readonly PayrollVoucherData? _singleData;
    private readonly IReadOnlyList<PayrollVoucherData> _allData;

    public FakeVoucherRepository(
        PayrollVoucherData? singleData,
        IReadOnlyList<PayrollVoucherData>? allData = null)
    {
        _singleData = singleData;
        _allData = allData ?? (singleData is not null ? [singleData] : []);
    }

    public Task<PayrollVoucherData?> GetVoucherDataAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken)
        => Task.FromResult(_singleData);

    public Task<IReadOnlyList<PayrollVoucherData>> GetAllVoucherDataAsync(Guid periodId, CancellationToken cancellationToken)
        => Task.FromResult(_allData);

    // Remaining interface members not needed for voucher tests — throw to detect unexpected calls
    public Task<AdminPayrollPeriodListResult> GetPeriodsAsync(AdminPayrollPeriodListFilter filter, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AdminPayrollPeriodDetail?> GetPeriodByIdAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid> CreatePeriodAsync(DateOnly startDate, DateOnly endDate, DateOnly cutoffDate, DateOnly paymentDate, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> ClosePeriodAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<AdminPayrollLineItem>> GetPeriodLinesAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AdminDeductionListResult> GetDeductionsAsync(Guid? nurseId, Guid? periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid> CreateDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> DeleteDeductionAsync(Guid deductionId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AdminCompensationAdjustmentListResult> GetAdjustmentsAsync(Guid? executionId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid> CreateAdjustmentAsync(CreateCompensationAdjustmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> DeleteAdjustmentAsync(Guid adjustmentId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<NursePeriodHistoryItem>> GetNursePeriodHistoryAsync(Guid nurseId, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<int> CountNurseLinesInOpenPeriodsAsync(Guid nurseId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<int> CountNurseLinesInClosedPeriodsAsync(Guid nurseId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<NursePeriodDetail?> GetNursePeriodDetailAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken) => throw new NotImplementedException();
}

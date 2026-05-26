using System.Text;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.AdminPortal.Payroll.Validation;
using NursingCareBackend.Infrastructure.Payroll;
using NursingCareBackend.Infrastructure.Payroll.Validation;
using QuestPDF.Infrastructure;

namespace NursingCareBackend.Application.Tests;

/// <summary>
/// Unit tests for the financial-output validation gate. The happy path uses a real voucher PDF
/// produced by <see cref="PayrollVoucherService"/> so render-integrity checks run against an
/// authentic document; failure paths feed crafted bytes/data directly. No DB or HTTP.
/// </summary>
public sealed class FinancialOutputValidatorTests
{
    private const string CompanyName = "Sol y Luna";
    private const string DocumentTitle = "COMPROBANTE DE PAGO";
    private const string PeriodLabel = "01/04/2026 al 30/04/2026";

    static FinancialOutputValidatorTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly IFinancialOutputValidator Validator = new FinancialOutputValidator();

    // ----- shared fixtures -----------------------------------------------------------------

    private static PayrollVoucherData BuildVoucherData() => new()
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
            new VoucherDeductionItem { Label = "Prestamo enero", DeductionTypeLabel = "Prestamo", Amount = 500m },
        ],
        TotalGross = 3400m,
        TotalTransport = 200m,
        TotalComplexity = 150m,
        TotalSupplies = 50m,
        TotalAdjustments = 0m,
        TotalDeductions = 500m,
        NetCompensation = 2900m, // 3000 + 200 + 150 + 50 + 0 - 500
    };

    private static FinancialDocumentData BuildDocumentData(PayrollVoucherData voucher) =>
        FinancialDocumentData.ForPayrollVoucher(voucher, CompanyName, DocumentTitle, PeriodLabel);

    private static byte[] GenerateRealVoucherPdf(PayrollVoucherData voucher)
    {
        var repo = new FakeValidatorVoucherRepository(voucher);
        var company = new FakeValidatorCompanyProvider(new CompanyInfo(CompanyName, null, null, null));
        var service = new PayrollVoucherService(repo, company);
        return service.GenerateVoucherAsync(voucher.PeriodId, voucher.NurseUserId).GetAwaiter().GetResult();
    }

    // ----- tests ---------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidVoucher_Passes()
    {
        var voucher = BuildVoucherData();
        var pdf = GenerateRealVoucherPdf(voucher);
        var data = BuildDocumentData(voucher);

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, pdf, data);

        Assert.True(result.IsValid, $"Expected valid but got: {result.ReasonSummary}");
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Validate_ReconciliationMismatch_FailsWithTotalsMismatch()
    {
        var voucher = BuildVoucherData();
        var pdf = GenerateRealVoucherPdf(voucher);

        // Net does not equal base+transport+complexity+supplies+adjustments-deductions.
        var tampered = new FinancialDocumentData
        {
            CompanyName = CompanyName,
            DocumentTitle = DocumentTitle,
            RecipientName = voucher.NurseDisplayName,
            PeriodLabel = PeriodLabel,
            BaseCompensation = 3000m,
            TransportIncentive = 200m,
            ComplexityBonus = 150m,
            MedicalSuppliesCompensation = 50m,
            Adjustments = 0m,
            Deductions = 500m,
            NetCompensation = 9999m, // wrong
        };

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, pdf, tampered);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Code == FinancialOutputValidator.CodeTotalsMismatch);
    }

    [Fact]
    public void Validate_NullBytes_FailsWithRenderEmpty()
    {
        var data = BuildDocumentData(BuildVoucherData());

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, null, data);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Code == FinancialOutputValidator.CodeRenderEmpty);
    }

    [Fact]
    public void Validate_NonPdfBytes_FailsWithRenderNotPdf()
    {
        var data = BuildDocumentData(BuildVoucherData());
        // 2KB of non-PDF content (passes the size gate, fails the magic-bytes gate).
        var bytes = Encoding.ASCII.GetBytes(new string('A', 2048));

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, bytes, data);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Code == FinancialOutputValidator.CodeRenderNotPdf);
    }

    [Fact]
    public void Validate_TooSmallBytes_FailsWithRenderTruncated()
    {
        var data = BuildDocumentData(BuildVoucherData());
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4 tiny"); // < 1KB

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, bytes, data);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Code == FinancialOutputValidator.CodeRenderTruncated);
    }

    [Fact]
    public void Validate_MissingRequiredToken_FailsWithMissingToken()
    {
        var voucher = BuildVoucherData();
        var pdf = GenerateRealVoucherPdf(voucher);

        // Company name the PDF does NOT contain → MISSING_TOKEN.
        var data = new FinancialDocumentData
        {
            CompanyName = "Empresa Inexistente XYZ",
            DocumentTitle = DocumentTitle,
            RecipientName = voucher.NurseDisplayName,
            PeriodLabel = PeriodLabel,
            BaseCompensation = 3000m,
            TransportIncentive = 200m,
            ComplexityBonus = 150m,
            MedicalSuppliesCompensation = 50m,
            Adjustments = 0m,
            Deductions = 500m,
            NetCompensation = 2900m,
        };

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, pdf, data);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Code == FinancialOutputValidator.CodeMissingToken);
    }

    [Fact]
    public void Validate_NegativeNet_FailsWithNegativeNet()
    {
        var voucher = BuildVoucherData();
        var pdf = GenerateRealVoucherPdf(voucher);

        // Components reconcile to a negative net (deductions exceed earnings).
        var data = new FinancialDocumentData
        {
            CompanyName = CompanyName,
            DocumentTitle = DocumentTitle,
            RecipientName = voucher.NurseDisplayName,
            PeriodLabel = PeriodLabel,
            BaseCompensation = 100m,
            TransportIncentive = 0m,
            ComplexityBonus = 0m,
            MedicalSuppliesCompensation = 0m,
            Adjustments = 0m,
            Deductions = 600m,
            NetCompensation = -500m, // 100 - 600
        };

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, pdf, data);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Code == FinancialOutputValidator.CodeNegativeNet);
    }

    [Fact]
    public void Validate_WithinCentTolerance_Passes()
    {
        var voucher = BuildVoucherData();
        var pdf = GenerateRealVoucherPdf(voucher);

        // Net off by exactly the tolerance (0.01) is accepted.
        var data = new FinancialDocumentData
        {
            CompanyName = CompanyName,
            DocumentTitle = DocumentTitle,
            RecipientName = voucher.NurseDisplayName,
            PeriodLabel = PeriodLabel,
            BaseCompensation = 3000m,
            TransportIncentive = 200m,
            ComplexityBonus = 150m,
            MedicalSuppliesCompensation = 50m,
            Adjustments = 0m,
            Deductions = 500m,
            NetCompensation = 2900.01m,
        };

        var result = Validator.Validate(FinancialDocumentKind.PayrollVoucher, pdf, data);

        Assert.True(result.IsValid, $"Expected valid but got: {result.ReasonSummary}");
    }
}

file sealed class FakeValidatorVoucherRepository(PayrollVoucherData voucher) : IAdminPayrollRepository
{
    public Task<PayrollVoucherData?> GetVoucherDataAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken)
        => Task.FromResult<PayrollVoucherData?>(voucher);

    public Task<IReadOnlyList<PayrollVoucherData>> GetAllVoucherDataAsync(Guid periodId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PayrollVoucherData>>([voucher]);

    public Task<AdminPayrollPeriodListResult> GetPeriodsAsync(AdminPayrollPeriodListFilter filter, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AdminPayrollPeriodDetail?> GetPeriodByIdAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid> CreatePeriodAsync(DateOnly startDate, DateOnly endDate, DateOnly cutoffDate, DateOnly paymentDate, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PeriodCloseResult> ClosePeriodAsync(Guid periodId, bool acknowledgeWarnings, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PeriodCloseWarnings> GetCloseWarningsAsync(Guid periodId, CancellationToken cancellationToken) => throw new NotImplementedException();
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

file sealed class FakeValidatorCompanyProvider(CompanyInfo info) : ICompanyInfoProvider
{
    public Task<CompanyInfo> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(info);
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Nurse;

[ApiController]
[Route("api/nurse/payroll")]
[Authorize(Roles = SystemRoles.Nurse)]
public sealed class NursePayrollController : ControllerBase
{
    private readonly INursePayrollRepository _payrollRepository;
    private readonly IPayrollVoucherService _voucherService;

    public NursePayrollController(
        INursePayrollRepository payrollRepository,
        IPayrollVoucherService voucherService)
    {
        _payrollRepository = payrollRepository;
        _voucherService = voucherService;
    }

    private Guid GetNurseUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? Guid.Parse(claim.Value) : Guid.Empty;
    }

    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NursePayrollSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var nurseId = GetNurseUserId();

        var openPeriods = await _payrollRepository.GetPeriodsAsync(
            new AdminPayrollPeriodListFilter(1, 1, "Open"),
            cancellationToken);

        var currentPeriod = openPeriods.Items.FirstOrDefault();

        decimal totalComp = 0m;
        if (currentPeriod != null)
        {
            var lines = await _payrollRepository.GetPeriodLinesAsync(currentPeriod.Id, cancellationToken);
            totalComp = lines.Where(l => l.NurseUserId == nurseId).Sum(l => l.NetCompensation);
        }

        var pendingCount = await _payrollRepository.CountNurseLinesInOpenPeriodsAsync(nurseId, cancellationToken);
        var completedCount = await _payrollRepository.CountNurseLinesInClosedPeriodsAsync(nurseId, cancellationToken);

        return Ok(new NursePayrollSummaryDto(
            nurseId,
            User.Identity?.Name ?? "Enfermera",
            currentPeriod?.Id,
            currentPeriod?.StartDate,
            currentPeriod?.EndDate,
            currentPeriod?.Status,
            totalComp,
            pendingCount,
            completedCount
        ));
    }

    [HttpGet("periods/{periodId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NursePayrollPeriodDetailDto>> GetPeriodDetail(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var nurseId = GetNurseUserId();
        var detail = await _payrollRepository.GetNursePeriodDetailAsync(periodId, nurseId, cancellationToken);

        if (detail is null) return NotFound();

        return Ok(new NursePayrollPeriodDetailDto(
            detail.PeriodId,
            detail.StartDate,
            detail.EndDate,
            detail.Status,
            detail.CutoffDate,
            detail.PaymentDate,
            detail.TotalGrossCompensation,
            detail.TotalDeductions,
            detail.TotalAdjustments,
            detail.NetCompensation,
            detail.Services.Select(s => new NursePayrollServiceRowDto(
                s.ServiceExecutionId,
                s.CareRequestId,
                s.ServiceDate,
                s.BaseCompensation,
                s.TransportIncentive,
                s.ComplexityBonus,
                s.MedicalSuppliesCompensation,
                s.AdjustmentsTotal,
                s.DeductionsTotal,
                s.NetCompensation
            )).ToList()
        ));
    }

    // GET /api/nurse/payroll/periods/{periodId}/voucher
    [HttpGet("periods/{periodId:guid}/voucher")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyVoucher(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var nurseId = GetNurseUserId();
        if (nurseId == Guid.Empty)
        {
            return this.ProblemResponse(
                StatusCodes.Status400BadRequest,
                "Sin identidad",
                "No se pudo determinar el usuario enfermera.");
        }

        try
        {
            var pdfBytes = await _voucherService.GenerateVoucherAsync(periodId, nurseId, cancellationToken);
            var fileName = $"comprobante-{periodId:N}.pdf";

            HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontraron datos de nomina para el periodo '{periodId}'.");
        }
    }

    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NursePeriodHistoryItemDto>>> GetHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var nurseId = GetNurseUserId();

        var history = await _payrollRepository.GetNursePeriodHistoryAsync(
            nurseId,
            Math.Max(1, pageNumber),
            Math.Clamp(pageSize, 1, 50),
            cancellationToken);

        var result = history.Select(h => new NursePeriodHistoryItemDto(
            h.PeriodId,
            h.StartDate,
            h.EndDate,
            h.Status,
            h.ServiceCount,
            h.TotalCompensation
        )).ToList();

        return Ok(result);
    }
}

public record NursePayrollSummaryDto(
    Guid NurseUserId,
    string NurseDisplayName,
    Guid? CurrentPeriodId,
    DateOnly? CurrentPeriodStartDate,
    DateOnly? CurrentPeriodEndDate,
    string? CurrentPeriodStatus,
    decimal TotalCompensationThisPeriod,
    int PendingPaymentsCount,
    int CompletedPaymentsCount
);

public record NursePayrollPeriodDetailDto(
    Guid PeriodId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateOnly CutoffDate,
    DateOnly PaymentDate,
    decimal TotalGrossCompensation,
    decimal TotalDeductions,
    decimal TotalAdjustments,
    decimal NetCompensation,
    List<NursePayrollServiceRowDto> Services
);

public record NursePayrollServiceRowDto(
    Guid ServiceExecutionId,
    Guid CareRequestId,
    DateOnly ServiceDate,
    decimal BaseCompensation,
    decimal TransportIncentive,
    decimal ComplexityBonus,
    decimal MedicalSuppliesCompensation,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation
);

public record NursePeriodHistoryItemDto(
    Guid PeriodId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int ServiceCount,
    decimal TotalCompensation
);

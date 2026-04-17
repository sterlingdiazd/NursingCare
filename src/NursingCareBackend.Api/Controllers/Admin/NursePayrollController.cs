using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Identity;
using System.Security.Claims;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/nurse/payroll")]
[Authorize(Roles = SystemRoles.Nurse)]
public sealed class NursePayrollController : ControllerBase
{
    private readonly IAdminPayrollRepository _payrollRepository;

    public NursePayrollController(IAdminPayrollRepository payrollRepository)
    {
        _payrollRepository = payrollRepository;
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

        var lines = currentPeriod != null 
            ? await _payrollRepository.GetPeriodLinesAsync(currentPeriod.Id, cancellationToken)
            : new List<AdminPayrollLineItem>();

        var nurseLines = lines.Where(l => l.NurseUserId == nurseId).ToList();
        var totalComp = nurseLines.Sum(l => l.NetCompensation);

        return Ok(new NursePayrollSummaryDto(
            nurseId.ToString(),
            User.Identity?.Name ?? "Enfermera",
            currentPeriod?.Id.ToString(),
            currentPeriod?.StartDate.ToString(),
            currentPeriod?.EndDate.ToString(),
            currentPeriod?.Status,
            totalComp,
            0,
            0
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
        var period = await _payrollRepository.GetPeriodByIdAsync(periodId, cancellationToken);
        
        if (period is null) return NotFound();

        var nurseLines = period.Lines.Where(l => l.NurseUserId == nurseId).ToList();

        return Ok(new NursePayrollPeriodDetailDto(
            period.Id.ToString(),
            period.StartDate.ToString(),
            period.EndDate.ToString(),
            period.Status,
            period.CutoffDate.ToString(),
            period.PaymentDate.ToString(),
            nurseLines.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation),
            nurseLines.Sum(l => l.DeductionsTotal),
            nurseLines.Sum(l => l.AdjustmentsTotal),
            nurseLines.Sum(l => l.NetCompensation),
            nurseLines.Select(l => new NursePayrollServiceRowDto(
                l.ServiceExecutionId?.ToString() ?? Guid.NewGuid().ToString(),
                "",
                "",
                l.BaseCompensation,
                l.TransportIncentive,
                l.ComplexityBonus,
                l.MedicalSuppliesCompensation,
                l.AdjustmentsTotal,
                l.DeductionsTotal,
                l.NetCompensation
            )).ToList()
        ));
    }

    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PayrollPeriodListItemDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var nurseId = GetNurseUserId();
        
        var periods = await _payrollRepository.GetPeriodsAsync(
            new AdminPayrollPeriodListFilter(1, 10, null),
            cancellationToken);

        var result = new List<PayrollPeriodListItemDto>();
        
        foreach (var period in periods.Items)
        {
            var lines = await _payrollRepository.GetPeriodLinesAsync(period.Id, cancellationToken);
            var nurseLines = lines.Where(l => l.NurseUserId == nurseId).ToList();
            
            result.Add(new PayrollPeriodListItemDto(
                period.Id.ToString(),
                period.StartDate.ToString(),
                period.EndDate.ToString(),
                period.Status,
                nurseLines.Count,
                nurseLines.Sum(l => l.NetCompensation)
            ));
        }

        return Ok(result);
    }
}

public record NursePayrollSummaryDto(
    string NurseUserId,
    string NurseDisplayName,
    string? CurrentPeriodId,
    string? CurrentPeriodStartDate,
    string? CurrentPeriodEndDate,
    string? CurrentPeriodStatus,
    decimal TotalCompensationThisPeriod,
    int PendingPaymentsCount,
    int CompletedPaymentsCount
);

public record NursePayrollPeriodDetailDto(
    string PeriodId,
    string StartDate,
    string EndDate,
    string Status,
    string CutoffDate,
    string PaymentDate,
    decimal TotalGrossCompensation,
    decimal TotalDeductions,
    decimal TotalAdjustments,
    decimal NetCompensation,
    List<NursePayrollServiceRowDto> Services
);

public record NursePayrollServiceRowDto(
    string ServiceExecutionId,
    string CareRequestId,
    string ServiceDate,
    decimal BaseCompensation,
    decimal TransportIncentive,
    decimal ComplexityBonus,
    decimal MedicalSuppliesCompensation,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation
);

public record PayrollPeriodListItemDto(
    string Id,
    string StartDate,
    string EndDate,
    string Status,
    int TotalNurses,
    decimal TotalCompensation
);
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/payroll")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminPayrollController : ControllerBase
{
    private readonly IAdminPayrollRepository _repository;
    private readonly IPayrollRecalculationService _recalculationService;
    private readonly IAdminPayrollOverrideRepository _overrideRepository;
    private readonly IPayrollVoucherService _voucherService;

    public AdminPayrollController(
        IAdminPayrollRepository repository,
        IPayrollRecalculationService recalculationService,
        IAdminPayrollOverrideRepository overrideRepository,
        IPayrollVoucherService voucherService)
    {
        _repository = repository;
        _recalculationService = recalculationService;
        _overrideRepository = overrideRepository;
        _voucherService = voucherService;
    }

    private Guid GetAdminUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? Guid.Parse(claim.Value) : Guid.Empty;
    }

    // GET /api/admin/payroll/periods
    [HttpGet("periods")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPayrollPeriodListResult>> GetPeriods(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetPeriodsAsync(
            new AdminPayrollPeriodListFilter(
                Math.Max(1, pageNumber),
                Math.Clamp(pageSize, 1, 100),
                status),
            cancellationToken);

        return Ok(result);
    }

    // GET /api/admin/payroll/periods/{id}
    [HttpGet("periods/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPayrollPeriodDetail>> GetPeriodById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await _repository.GetPeriodByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontro el periodo de nomina con id '{id}'.");
        }

        return Ok(detail);
    }

    // POST /api/admin/payroll/periods
    [HttpPost("periods")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePeriod(
        [FromBody] CreatePayrollPeriodRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EndDate < request.StartDate)
        {
            return this.ProblemResponse(
                StatusCodes.Status400BadRequest,
                "Rango de fechas invalido",
                "La fecha de fin debe ser igual o posterior a la fecha de inicio.");
        }

        try
        {
            var id = await _repository.CreatePeriodAsync(
                request.StartDate,
                request.EndDate,
                request.CutoffDate,
                request.PaymentDate,
                cancellationToken);

            return CreatedAtAction(nameof(GetPeriodById), new { id }, new { id });
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Datos invalidos", ex.Message);
        }
    }

    // PATCH /api/admin/payroll/periods/{id}/close
    [HttpPatch("periods/{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClosePeriod(Guid id, CancellationToken cancellationToken)
    {
        var found = await _repository.ClosePeriodAsync(id, cancellationToken);

        if (!found)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontro el periodo de nomina con id '{id}'.");
        }

        return NoContent();
    }

    // GET /api/admin/payroll/periods/{id}/lines
    [HttpGet("periods/{id:guid}/lines")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPayrollLineItem>>> GetPeriodLines(
        Guid id,
        CancellationToken cancellationToken)
    {
        var lines = await _repository.GetPeriodLinesAsync(id, cancellationToken);
        return Ok(lines);
    }

    // GET /api/admin/payroll/periods/{id}/export
    [HttpGet("periods/{id:guid}/export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPeriod(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _repository.GetPeriodByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontro el periodo con id '{id}'.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Periodo,Inicio,Fin,Corte,Pago,Estado");
        sb.AppendLine($"{EscapeCsv(detail.StartDate + " al " + detail.EndDate)},{detail.StartDate:yyyy-MM-dd},{detail.EndDate:yyyy-MM-dd},{detail.CutoffDate:yyyy-MM-dd},{detail.PaymentDate:yyyy-MM-dd},{EscapeCsv(detail.Status)}");
        sb.AppendLine();
        sb.AppendLine("--- Resumen por Enfermera ---");
        sb.AppendLine("Enfermera,Lineas,Bruto,Transporte,Ajustes,Deducciones,Neto");
        foreach (var row in detail.StaffSummary)
            sb.AppendLine($"{EscapeCsv(row.NurseDisplayName)},{row.LineCount},{row.GrossCompensation:F2},{row.TransportIncentives:F2},{row.AdjustmentsTotal:F2},{row.DeductionsTotal:F2},{row.NetCompensation:F2}");
        sb.AppendLine();
        sb.AppendLine("--- Lineas de Nomina ---");
        sb.AppendLine("Enfermera,Descripcion,Base,Transporte,Complejidad,Insumos,Ajustes,Deducciones,Neto");
        foreach (var line in detail.Lines)
            sb.AppendLine($"{EscapeCsv(line.NurseDisplayName)},{EscapeCsv(line.Description)},{line.BaseCompensation:F2},{line.TransportIncentive:F2},{line.ComplexityBonus:F2},{line.MedicalSuppliesCompensation:F2},{line.AdjustmentsTotal:F2},{line.DeductionsTotal:F2},{line.NetCompensation:F2}");

        HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"nomina-periodo-{id:N}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // GET /api/admin/payroll/deductions
    [HttpGet("deductions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDeductionListResult>> GetDeductions(
        [FromQuery] Guid? nurseId,
        [FromQuery] Guid? periodId,
        CancellationToken cancellationToken)
    {
        var result = await _repository.GetDeductionsAsync(nurseId, periodId, cancellationToken);
        return Ok(result);
    }

    // POST /api/admin/payroll/deductions
    [HttpPost("deductions")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDeduction([FromBody] CreateDeductionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _repository.CreateDeductionAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetDeductions), new { }, new { id });
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Datos invalidos", ex.Message);
        }
    }

    // DELETE /api/admin/payroll/deductions/{id}
    [HttpDelete("deductions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDeduction(Guid id, CancellationToken cancellationToken)
    {
        var found = await _repository.DeleteDeductionAsync(id, cancellationToken);

        if (!found)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Deduccion no encontrada",
                $"No se encontro la deduccion con id '{id}'.");
        }

        return NoContent();
    }

    // GET /api/admin/payroll/adjustments
    [HttpGet("adjustments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminCompensationAdjustmentListResult>> GetAdjustments(
        [FromQuery] Guid? executionId,
        CancellationToken cancellationToken)
    {
        var result = await _repository.GetAdjustmentsAsync(executionId, cancellationToken);
        return Ok(result);
    }

    // POST /api/admin/payroll/adjustments
    [HttpPost("adjustments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAdjustment([FromBody] CreateCompensationAdjustmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _repository.CreateAdjustmentAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetAdjustments), new { }, new { id });
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Datos invalidos", ex.Message);
        }
    }

    // DELETE /api/admin/payroll/adjustments/{id}
    [HttpDelete("adjustments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAdjustment(Guid id, CancellationToken cancellationToken)
    {
        var found = await _repository.DeleteAdjustmentAsync(id, cancellationToken);

        if (!found)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Ajuste no encontrado",
                $"No se encontro el ajuste de compensacion con id '{id}'.");
        }

        return NoContent();
    }

    // GET /api/admin/payroll/mobile-summary
    [HttpGet("mobile-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetMobileSummary(CancellationToken cancellationToken)
    {
        var openPeriods = await _repository.GetPeriodsAsync(
            new AdminPayrollPeriodListFilter(1, 1, "Open"),
            cancellationToken);
        
        var closedPeriods = await _repository.GetPeriodsAsync(
            new AdminPayrollPeriodListFilter(1, 5, "Closed"),
            cancellationToken);

        decimal currentTotal = 0;
        var allNurseIds = new List<Guid>();
        
        if (openPeriods.Items.FirstOrDefault() is var openPeriod && openPeriod != null)
        {
            var lines = await _repository.GetPeriodLinesAsync(openPeriod.Id, cancellationToken);
            currentTotal = lines.Sum(l => l.NetCompensation);
            allNurseIds.AddRange(lines.Select(l => l.NurseUserId).Distinct());
        }

        foreach (var period in closedPeriods.Items)
        {
            var lines = await _repository.GetPeriodLinesAsync(period.Id, cancellationToken);
            allNurseIds.AddRange(lines.Select(l => l.NurseUserId).Distinct());
        }

        return Ok(new
        {
            OpenPeriodsCount = openPeriods.TotalCount,
            ClosedPeriodsCount = closedPeriods.TotalCount,
            TotalCompensationCurrentPeriod = currentTotal,
            ActiveNursesCount = allNurseIds.Distinct().Count(),
            RecentPeriods = closedPeriods.Items.Select(p => new
            {
                p.Id,
                p.StartDate,
                p.EndDate,
                p.Status,
                p.LineCount
            })
        });
    }

    // POST /api/admin/payroll/recalculate
    [HttpPost("recalculate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecalculatePayroll(
        [FromBody] RecalculatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = GetAdminUserId();
        if (adminId == Guid.Empty)
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Sin identidad", "No se pudo determinar el usuario administrador.");

        var result = await _recalculationService.RecalculateAsync(adminId, request, cancellationToken);
        return Ok(result);
    }

    // POST /api/admin/payroll/lines/{lineId}/override
    [HttpPost("lines/{lineId:guid}/override")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitOverride(
        Guid lineId,
        [FromBody] SubmitOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = GetAdminUserId();
        if (adminId == Guid.Empty)
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Sin identidad", "No se pudo determinar el usuario administrador.");

        var requestWithLineId = request with { LineId = lineId };

        try
        {
            var overrideId = await _overrideRepository.SubmitOverrideAsync(requestWithLineId, adminId, DateTime.UtcNow, cancellationToken);
            return Created($"/api/admin/payroll/lines/{lineId}/override", new { overrideId });
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Datos invalidos", ex.Message);
        }
    }

    // POST /api/admin/payroll/lines/{lineId}/override/approve
    [HttpPost("lines/{lineId:guid}/override/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveOverride(
        Guid lineId,
        CancellationToken cancellationToken)
    {
        var adminId = GetAdminUserId();
        if (adminId == Guid.Empty)
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Sin identidad", "No se pudo determinar el usuario administrador.");

        var (found, error) = await _overrideRepository.ApproveOverrideAsync(lineId, adminId, DateTime.UtcNow, cancellationToken);

        if (!found)
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Override no encontrado", $"No hay una solicitud de compensacion pendiente para la linea '{lineId}'.");

        if (error is not null)
            return this.ProblemResponse(StatusCodes.Status403Forbidden, "No autorizado", "Not authorized to approve this override.");

        return NoContent();
    }

    // GET /api/admin/payroll/periods/{periodId}/voucher/{nurseId}
    [HttpGet("periods/{periodId:guid}/voucher/{nurseId:guid}")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNurseVoucher(
        Guid periodId,
        Guid nurseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var pdfBytes = await _voucherService.GenerateVoucherAsync(periodId, nurseId, cancellationToken);
            var fileName = $"voucher-{nurseId:N}-{periodId:N}.pdf";

            HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo o enfermera no encontrado",
                $"No se encontraron datos de nomina para el periodo '{periodId}' y la enfermera '{nurseId}'.");
        }
    }

    // GET /api/admin/payroll/periods/{periodId}/vouchers/zip
    [HttpGet("periods/{periodId:guid}/vouchers/zip")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBulkVouchersZip(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        try
        {
            var zipBytes = await _voucherService.GenerateBulkVouchersZipAsync(periodId, cancellationToken);

            var period = await _repository.GetPeriodByIdAsync(periodId, cancellationToken);
            var fileName = period is not null
                ? $"vouchers-{period.StartDate:yyyyMMdd}-{period.EndDate:yyyyMMdd}.zip"
                : $"vouchers-{periodId:N}.zip";

            HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(zipBytes, "application/zip", fileName);
        }
        catch (InvalidOperationException)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontraron datos de nomina para el periodo '{periodId}'.");
        }
    }

    private static string EscapeCsv(object? value)
    {
        if (value is null) return "\"\"";
        var text = value.ToString() ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}

public sealed class CreatePayrollPeriodRequest
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly CutoffDate { get; set; }
    public DateOnly PaymentDate { get; set; }
}

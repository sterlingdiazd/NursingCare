using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Localization;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Exceptions;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Api.Security;
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
    private readonly IPayrollReportExportService _reportExportService;
    private readonly IScheduledDeductionService _scheduledDeductionService;
    private readonly IAuthRateLimiter _rateLimiter;

    public AdminPayrollController(
        IAdminPayrollRepository repository,
        IPayrollRecalculationService recalculationService,
        IAdminPayrollOverrideRepository overrideRepository,
        IPayrollVoucherService voucherService,
        IPayrollReportExportService reportExportService,
        IScheduledDeductionService scheduledDeductionService,
        IAuthRateLimiter rateLimiter)
    {
        _repository = repository;
        _recalculationService = recalculationService;
        _overrideRepository = overrideRepository;
        _voucherService = voucherService;
        _reportExportService = reportExportService;
        _scheduledDeductionService = scheduledDeductionService;
        _rateLimiter = rateLimiter;
    }

    private Guid GetAdminUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var adminUserId)
            ? adminUserId
            : Guid.Empty;
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
                Messages.Get("errors.periodo_no_encontrado"),
                $"No se encontró el período de nómina con id '{id}'.");
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
        if (ValidatePeriodDates(request) is { } dateError)
        {
            return dateError;
        }

        try
        {
            var id = await _repository.CreatePeriodAsync(
                request.StartDate,
                request.EndDate,
                request.CutoffDate,
                request.PaymentDate,
                cancellationToken);

            // Generate any installments now due for active scheduled deductions in this new period.
            await _scheduledDeductionService.EnsureInstallmentsForOpenPeriodsAsync(cancellationToken);

            return CreatedAtAction(nameof(GetPeriodById), new { id }, new { id });
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Safety net for the (StartDate, EndDate) unique index under a race.
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.periodo_solapado"), Messages.Get("errors.periodo_solapado_detalle"));
        }
    }

    // PATCH /api/admin/payroll/periods/{id}/close
    [HttpPatch("periods/{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClosePeriod(Guid id, CancellationToken cancellationToken)
    {
        var result = await _repository.ClosePeriodAsync(id, cancellationToken);

        if (result == PeriodCloseResult.NotFound)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                Messages.Get("errors.periodo_no_encontrado"),
                $"No se encontró el período de nómina con id '{id}'.");
        }

        if (result == PeriodCloseResult.Empty)
        {
            return this.ProblemResponse(
                StatusCodes.Status409Conflict,
                Messages.Get("errors.periodo_vacio"),
                Messages.Get("errors.periodo_vacio_detalle"));
        }

        // Closing settles each scheduled-deduction installment that lived in this period.
        await _scheduledDeductionService.SettlePeriodInstallmentsAsync(id, cancellationToken);

        return NoContent();
    }

    // PUT /api/admin/payroll/periods/{id}
    [HttpPut("periods/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePeriod(
        Guid id,
        [FromBody] CreatePayrollPeriodRequest request,
        CancellationToken cancellationToken)
    {
        if (ValidatePeriodDates(request) is { } dateError)
        {
            return dateError;
        }

        var result = await _repository.UpdatePeriodAsync(
            id, request.StartDate, request.EndDate, request.CutoffDate, request.PaymentDate, cancellationToken);

        return MapPeriodMutation(result, id) ?? NoContent();
    }

    // DELETE /api/admin/payroll/periods/{id}
    [HttpDelete("periods/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePeriod(Guid id, CancellationToken cancellationToken)
    {
        var result = await _repository.DeletePeriodAsync(id, cancellationToken);
        return MapPeriodMutation(result, id) ?? NoContent();
    }

    // Standard period date rules (start ≤ end, start ≤ cutoff, cutoff ≤ payment).
    // Returns a 400 problem-details response on violation, or null when the dates are valid.
    private IActionResult? ValidatePeriodDates(CreatePayrollPeriodRequest request)
    {
        if (request.EndDate < request.StartDate)
        {
            return this.ProblemResponse(
                StatusCodes.Status400BadRequest,
                Messages.Get("errors.rango_fechas_invalido"),
                Messages.Get("errors.rango_fechas_detalle"));
        }

        if (request.CutoffDate < request.StartDate || request.PaymentDate < request.CutoffDate)
        {
            return this.ProblemResponse(
                StatusCodes.Status400BadRequest,
                Messages.Get("errors.rango_fechas_invalido"),
                Messages.Get("errors.fechas_periodo_detalle"));
        }

        return null;
    }

    // Maps a rejected period edit/delete to a problem-details response. Returns null on success.
    private IActionResult? MapPeriodMutation(PeriodMutationResult result, Guid id) => result switch
    {
        PeriodMutationResult.NotFound => this.ProblemResponse(
            StatusCodes.Status404NotFound,
            Messages.Get("errors.periodo_no_encontrado"),
            $"No se encontró el período de nómina con id '{id}'."),
        PeriodMutationResult.Closed => this.ProblemResponse(
            StatusCodes.Status409Conflict,
            Messages.Get("errors.periodo_cerrado"),
            Messages.Get("errors.periodo_cerrado_no_modificable")),
        PeriodMutationResult.InUse => this.ProblemResponse(
            StatusCodes.Status409Conflict,
            Messages.Get("errors.periodo_en_uso"),
            Messages.Get("errors.periodo_en_uso_detalle")),
        PeriodMutationResult.Overlap => this.ProblemResponse(
            StatusCodes.Status400BadRequest,
            Messages.Get("errors.periodo_solapado"),
            Messages.Get("errors.periodo_solapado_detalle")),
        _ => null,
    };

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

    // GET /api/admin/payroll/periods/{id}/report/pdf
    [HttpGet("periods/{id:guid}/report/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportProfessionalReportPdf(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _repository.GetPeriodByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontro el periodo con id '{id}'.");
        }

        HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
        var bytes = _reportExportService.GeneratePdf(detail);
        return File(bytes, "application/pdf", $"payroll-report-{id:N}.pdf");
    }

    // GET /api/admin/payroll/periods/{id}/report/xlsx
    [HttpGet("periods/{id:guid}/report/xlsx")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportProfessionalReportXlsx(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _repository.GetPeriodByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontro el periodo con id '{id}'.");
        }

        HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
        var bytes = _reportExportService.GenerateXlsx(detail);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"payroll-report-{id:N}.xlsx");
    }

    // GET /api/admin/payroll/periods/{id}/report/html
    [HttpGet("periods/{id:guid}/report/html")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportProfessionalReportHtml(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _repository.GetPeriodByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Periodo no encontrado",
                $"No se encontro el periodo con id '{id}'.");
        }

        HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
        var bytes = _reportExportService.GenerateHtml(detail);
        return File(bytes, "text/html; charset=utf-8", $"payroll-report-{id:N}.html");
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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDeduction([FromBody] CreateDeductionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _repository.CreateDeductionAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetDeductions), new { }, new { id });
        }
        catch (PayrollPeriodClosedException ex)
        {
            return this.ProblemResponse(StatusCodes.Status409Conflict, Messages.Get("errors.periodo_cerrado"), ex.Message);
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), ex.Message);
        }
    }

    // DELETE /api/admin/payroll/deductions/{id}
    [HttpDelete("deductions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDeduction(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var found = await _repository.DeleteDeductionAsync(id, cancellationToken);

            if (!found)
            {
                return this.ProblemResponse(
                    StatusCodes.Status404NotFound,
                    Messages.Get("errors.deduccion_no_encontrada"),
                    $"No se encontró la deducción con id '{id}'.");
            }

            return NoContent();
        }
        catch (PayrollPeriodClosedException ex)
        {
            return this.ProblemResponse(StatusCodes.Status409Conflict, Messages.Get("errors.periodo_cerrado"), ex.Message);
        }
    }

    // PUT /api/admin/payroll/deductions/{id}
    [HttpPut("deductions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDeduction(Guid id, [FromBody] UpdateDeductionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var found = await _repository.UpdateDeductionAsync(id, request, cancellationToken);

            if (!found)
            {
                return this.ProblemResponse(
                    StatusCodes.Status404NotFound,
                    Messages.Get("errors.deduccion_no_encontrada"),
                    $"No se encontró la deducción con id '{id}'.");
            }

            return NoContent();
        }
        catch (PayrollPeriodClosedException ex)
        {
            return this.ProblemResponse(StatusCodes.Status409Conflict, Messages.Get("errors.periodo_cerrado"), ex.Message);
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status409Conflict, "Operación no permitida", ex.Message);
        }
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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAdjustment([FromBody] CreateCompensationAdjustmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _repository.CreateAdjustmentAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetAdjustments), new { }, new { id });
        }
        catch (PayrollPeriodClosedException ex)
        {
            return this.ProblemResponse(StatusCodes.Status409Conflict, Messages.Get("errors.periodo_cerrado"), ex.Message);
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), ex.Message);
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
                Messages.Get("errors.ajuste_no_encontrado"),
                $"No se encontró el ajuste de compensación con id '{id}'.");
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
            // Use the period-level staff summary so deductions are netted once, not per line.
            var detail = await _repository.GetPeriodByIdAsync(openPeriod.Id, cancellationToken);
            if (detail != null)
            {
                currentTotal = detail.StaffSummary.Sum(s => s.NetCompensation);
                allNurseIds.AddRange(detail.StaffSummary.Select(s => s.NurseUserId));
            }
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
            return Unauthorized();

        // SEC-001: Rate-limit recalculation — 5 requests per minute per admin user
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateCheck = _rateLimiter.Check($"recalculate:{adminId}", clientIp, 5, TimeSpan.FromMinutes(1));
        if (!rateCheck.IsAllowed)
        {
            HttpContext.Response.Headers.Append("Retry-After", Math.Max(1, (int)Math.Ceiling(rateCheck.RetryAfter.TotalSeconds)).ToString());
            return this.ProblemResponse(StatusCodes.Status429TooManyRequests, Messages.Get("errors.rate_limit_recalculo"),
                $"Ha excedido el límite de recálculos. Intente nuevamente en {Math.Max(1, (int)Math.Ceiling(rateCheck.RetryAfter.TotalSeconds))} segundos.");
        }

        try
        {
            var result = await _recalculationService.RecalculateAsync(adminId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), ex.Message);
        }
    }

    // POST /api/admin/payroll/lines/{lineId}/override
    [HttpPost("lines/{lineId:guid}/override")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitOverride(
        Guid lineId,
        [FromBody] SubmitOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = GetAdminUserId();
        if (adminId == Guid.Empty)
            return Unauthorized();

        var requestWithLineId = request with { LineId = lineId };

        try
        {
            var overrideId = await _overrideRepository.SubmitOverrideAsync(requestWithLineId, adminId, DateTime.UtcNow, cancellationToken);
            return Created($"/api/admin/payroll/lines/{lineId}/override", new { overrideId });
        }
        catch (PayrollPeriodClosedException ex)
        {
            return this.ProblemResponse(StatusCodes.Status409Conflict, Messages.Get("errors.periodo_cerrado"), ex.Message);
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), ex.Message);
        }
    }

    // POST /api/admin/payroll/lines/{lineId}/override/approve
    [HttpPost("lines/{lineId:guid}/override/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveOverride(
        Guid lineId,
        CancellationToken cancellationToken)
    {
        var adminId = GetAdminUserId();
        if (adminId == Guid.Empty)
            return Unauthorized();

        try
        {
            var (found, error) = await _overrideRepository.ApproveOverrideAsync(lineId, adminId, DateTime.UtcNow, cancellationToken);

            if (!found)
                return this.ProblemResponse(StatusCodes.Status404NotFound, Messages.Get("errors.override_no_encontrado"), $"No hay una solicitud de compensación pendiente para la línea '{lineId}'.");

            if (error is not null)
                return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), error);

            return NoContent();
        }
        catch (PayrollPeriodClosedException ex)
        {
            return this.ProblemResponse(StatusCodes.Status409Conflict, Messages.Get("errors.periodo_cerrado"), ex.Message);
        }
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

            var period = await _repository.GetPeriodByIdAsync(periodId, cancellationToken);
            var fileName = period is not null
                ? $"voucher-{period.StartDate:yyyyMMdd}-{period.EndDate:yyyyMMdd}.pdf"
                : "voucher-de-pago.pdf";

            HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (VoucherNotFoundException)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                Messages.Get("errors.periodo_enfermera_no_encontrado"),
                Messages.Get("errors.periodo_enfermera_no_encontrado_detalle"));
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
                : "vouchers.zip";

            HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(zipBytes, "application/zip", fileName);
        }
        catch (VoucherNotFoundException)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                Messages.Get("errors.periodo_zip_no_encontrado"),
                Messages.Get("errors.periodo_zip_no_encontrado_detalle"));
        }
    }

    // GET /api/admin/payroll/periods/{periodId}/nurse-detail/{nurseUserId}
    [HttpGet("periods/{periodId:guid}/nurse-detail/{nurseUserId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNursePayrollDetail(
        Guid periodId,
        Guid nurseUserId,
        CancellationToken cancellationToken)
    {
        var detail = await _repository.GetNursePeriodDetailAsync(periodId, nurseUserId, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                Messages.Get("errors.detalle_no_encontrado"),
                $"No se encontraron datos de nómina para el período '{periodId}' y la enfermera '{nurseUserId}'.");
        }

        return Ok(detail);
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

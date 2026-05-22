using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Localization;
using NursingCareBackend.Api.Security;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/payroll/scheduled-deductions")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminScheduledDeductionsController : ControllerBase
{
    private readonly IAdminScheduledDeductionRepository _repository;
    private readonly IScheduledDeductionService _generationService;

    public AdminScheduledDeductionsController(
        IAdminScheduledDeductionRepository repository,
        IScheduledDeductionService generationService)
    {
        _repository = repository;
        _generationService = generationService;
    }

    private Guid GetAdminUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ScheduledDeductionListResult>> List(
        [FromQuery] Guid? nurseId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => Ok(await _repository.GetAsync(nurseId, status, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduledDeductionDetail>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _repository.GetByIdAsync(id, cancellationToken);
        return detail is null ? NotFoundProblem(id) : Ok(detail);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateScheduledDeductionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _repository.CreateAsync(request, GetAdminUserId(), cancellationToken);
            // Generate any installment already due in the current open period(s).
            await _generationService.EnsureInstallmentsForOpenPeriodsAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), ex.Message);
        }
    }

    [HttpPost("{id:guid}/payoff")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Payoff(Guid id, CancellationToken cancellationToken)
        => MutateAsync(() => _repository.PayoffAsync(id, cancellationToken), id);

    [HttpPut("{id:guid}/reschedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleScheduledDeductionRequest request, CancellationToken cancellationToken)
        => MutateAsync(() => _repository.RescheduleAsync(id, request, cancellationToken), id);

    [HttpPost("{id:guid}/skip")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Skip(Guid id, [FromBody] SkipInstallmentRequest request, CancellationToken cancellationToken)
        => MutateAsync(() => _repository.SkipInstallmentAsync(id, request.PayrollPeriodId, cancellationToken), id);

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Cancel(Guid id, [FromBody] CancelScheduledDeductionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Task.FromResult<IActionResult>(this.ProblemResponse(
                StatusCodes.Status400BadRequest, Messages.Get("errors.datos_invalidos"), "El motivo de cancelación es obligatorio."));

        return MutateAsync(() => _repository.CancelAsync(id, request.Reason, GetAdminUserId(), cancellationToken), id);
    }

    private async Task<IActionResult> MutateAsync(Func<Task<bool>> action, Guid id)
    {
        try
        {
            var found = await action();
            return found ? NoContent() : NotFoundProblem(id);
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

    private ObjectResult NotFoundProblem(Guid id) => this.ProblemResponse(
        StatusCodes.Status404NotFound,
        "Deducción programada no encontrada",
        $"No se encontró la deducción programada con id '{id}'.");
}

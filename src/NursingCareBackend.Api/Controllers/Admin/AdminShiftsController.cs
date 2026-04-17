using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/shifts")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminShiftsController : ControllerBase
{
    private readonly IAdminShiftRepository _repository;

    public AdminShiftsController(IAdminShiftRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminShiftListResult>> GetShifts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? nurseId = null,
        [FromQuery] Guid? careRequestId = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        DateOnly? parsedStartDate = null;
        DateOnly? parsedEndDate = null;

        if (!string.IsNullOrEmpty(startDate) && DateOnly.TryParse(startDate, out var sd))
            parsedStartDate = sd;
        if (!string.IsNullOrEmpty(endDate) && DateOnly.TryParse(endDate, out var ed))
            parsedEndDate = ed;

        var result = await _repository.GetShiftsAsync(
            new AdminShiftListFilter(
                Math.Max(1, pageNumber),
                Math.Clamp(pageSize, 1, 100),
                nurseId,
                careRequestId,
                parsedStartDate,
                parsedEndDate,
                status),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminShiftRecordDetail>> GetShiftById(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _repository.GetShiftByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Turno no encontrado",
                $"No se encontro el turno con id '{id}'.");
        }

        return Ok(detail);
    }

    [HttpGet("{id:guid}/changes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminShiftChangeItem>>> GetShiftChanges(Guid id, CancellationToken cancellationToken)
    {
        var changes = await _repository.GetShiftChangesAsync(id, cancellationToken);
        return Ok(changes);
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/payroll/compensation-rules")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminCompensationRulesController : ControllerBase
{
    private readonly IAdminCompensationRulesRepository _repository;

    public AdminCompensationRulesController(IAdminCompensationRulesRepository repository)
    {
        _repository = repository;
    }

    // GET /api/admin/payroll/compensation-rules
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminCompensationRuleListResult>> GetRules(CancellationToken cancellationToken)
    {
        var result = await _repository.GetRulesAsync(cancellationToken);
        return Ok(result);
    }

    // GET /api/admin/payroll/compensation-rules/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminCompensationRuleDetail>> GetRuleById(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _repository.GetRuleByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Regla no encontrada",
                $"No se encontro la regla de compensacion con id '{id}'.");
        }

        return Ok(detail);
    }

    // POST /api/admin/payroll/compensation-rules
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRule([FromBody] CreateCompensationRuleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _repository.CreateRuleAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetRuleById), new { id }, new { id });
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Datos invalidos", ex.Message);
        }
    }

    // PUT /api/admin/payroll/compensation-rules/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateCompensationRuleRequest request, CancellationToken cancellationToken)
    {
        var found = await _repository.UpdateRuleAsync(id, request, cancellationToken);

        if (!found)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Regla no encontrada",
                $"No se encontro la regla de compensacion con id '{id}'.");
        }

        return NoContent();
    }

    // DELETE /api/admin/payroll/compensation-rules/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateRule(Guid id, CancellationToken cancellationToken)
    {
        var found = await _repository.DeactivateRuleAsync(id, cancellationToken);

        if (!found)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Regla no encontrada",
                $"No se encontro la regla de compensacion con id '{id}'.");
        }

        return NoContent();
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.AdminPortal.Finance;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/finance")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminFinanceController : ControllerBase
{
    private readonly IAdminFinanceRepository _repository;

    public AdminFinanceController(IAdminFinanceRepository repository)
    {
        _repository = repository;
    }

    // GET /api/admin/finance/overview?from=&to=  (defaults to the current month)
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FinanceOverview>> GetOverview(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = from ?? new DateOnly(today.Year, today.Month, 1);
        var t = to ?? today;
        var overview = await _repository.GetOverviewAsync(f, t, cancellationToken);
        return Ok(overview);
    }

    // GET /api/admin/finance/detail?metric=&from=&to=  (source records behind a dashboard metric)
    [HttpGet("detail")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FinanceDetail>> GetDetail(
        [FromQuery] string metric,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = from ?? new DateOnly(today.Year, today.Month, 1);
        var t = to ?? today;
        var detail = await _repository.GetDetailAsync(metric, f, t, cancellationToken);
        if (detail is null)
        {
            return Problem(title: "Métrica desconocida", detail: $"No se reconoce la métrica '{metric}'.", statusCode: StatusCodes.Status400BadRequest);
        }
        return Ok(detail);
    }
}

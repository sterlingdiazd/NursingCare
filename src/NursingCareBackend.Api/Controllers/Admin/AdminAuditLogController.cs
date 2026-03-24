using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminAuditLogController : ControllerBase
{
  private readonly IAuditLogQueryService _queryService;

  public AdminAuditLogController(IAuditLogQueryService queryService)
  {
    _queryService = queryService;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AuditLogSearchResult>> Search(
    [FromQuery] Guid? actorUserId,
    [FromQuery] string? action,
    [FromQuery] string? entityType,
    [FromQuery] string? entityId,
    [FromQuery] DateTime? fromDate,
    [FromQuery] DateTime? toDate,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 50,
    CancellationToken cancellationToken = default)
  {
    if (pageSize < 1 || pageSize > 100)
    {
      pageSize = 50;
    }

    if (pageNumber < 1)
    {
      pageNumber = 1;
    }

    var request = new AuditLogSearchRequest(
      actorUserId,
      action,
      entityType,
      entityId,
      fromDate,
      toDate,
      pageNumber,
      pageSize);

    var result = await _queryService.SearchAsync(request, cancellationToken);
    return Ok(result);
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AuditLogDetail>> GetById(Guid id, CancellationToken cancellationToken = default)
  {
    var detail = await _queryService.GetByIdAsync(id, cancellationToken);
    if (detail is null)
    {
      return NotFound();
    }

    return Ok(detail);
  }
}

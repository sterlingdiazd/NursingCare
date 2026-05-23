using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/action-items")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminActionItemsController : ControllerBase
{
  private readonly GetAdminActionQueueHandler _handler;

  public AdminActionItemsController(GetAdminActionQueueHandler handler)
  {
    _handler = handler;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> Get(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = AdminActionQueueFilter.DefaultPageSize,
    CancellationToken cancellationToken = default)
  {
    var filter = AdminActionQueueFilter.Sanitized(page, pageSize);
    var result = await _handler.Handle(filter, cancellationToken);
    return Ok(result);
  }
}

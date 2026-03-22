using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminDashboardController : ControllerBase
{
  private readonly GetAdminDashboardHandler _handler;

  public AdminDashboardController(GetAdminDashboardHandler handler)
  {
    _handler = handler;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> Get(CancellationToken cancellationToken)
  {
    var snapshot = await _handler.Handle(cancellationToken);
    return Ok(snapshot);
  }
}

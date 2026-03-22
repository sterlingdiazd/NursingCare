using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Users;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/admin-accounts")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminAccountsController : ControllerBase
{
  private readonly IAdminAccountProvisioningService _adminAccountProvisioningService;

  public AdminAccountsController(IAdminAccountProvisioningService adminAccountProvisioningService)
  {
    _adminAccountProvisioningService = adminAccountProvisioningService;
  }

  [HttpPost]
  [ProducesResponseType(typeof(AdminUserDetail), StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<ActionResult<AdminUserDetail>> Create(
    [FromBody] CreateAdminAccountApiRequest request,
    CancellationToken cancellationToken)
  {
    var actorUserId = ResolveActorUserId();
    if (actorUserId is null)
    {
      return this.ProblemResponse(
        StatusCodes.Status401Unauthorized,
        "No autorizado",
        "La sesion actual no incluye un identificador administrativo valido.");
    }

    var detail = await _adminAccountProvisioningService.CreateAsync(
      new CreateAdminAccountRequest(
        request.Name,
        request.LastName,
        request.IdentificationNumber,
        request.Phone,
        request.Email,
        request.Password,
        request.ConfirmPassword),
      actorUserId.Value,
      cancellationToken);

    return StatusCode(StatusCodes.Status201Created, detail);
  }

  private Guid? ResolveActorUserId()
  {
    var rawValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawValue, out var userId) ? userId : null;
  }
}

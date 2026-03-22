using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.ErrorHandling;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Users;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
  private readonly GetAdminUsersHandler _getAdminUsersHandler;
  private readonly GetAdminUserDetailHandler _getAdminUserDetailHandler;
  private readonly IAdminUserManagementService _adminUserManagementService;

  public AdminUsersController(
    GetAdminUsersHandler getAdminUsersHandler,
    GetAdminUserDetailHandler getAdminUserDetailHandler,
    IAdminUserManagementService adminUserManagementService)
  {
    _getAdminUsersHandler = getAdminUsersHandler;
    _getAdminUserDetailHandler = getAdminUserDetailHandler;
    _adminUserManagementService = adminUserManagementService;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<IReadOnlyList<AdminUserListItem>>> Get(
    [FromQuery] string? search,
    [FromQuery] string? role,
    [FromQuery] string? profileType,
    [FromQuery] string? status,
    CancellationToken cancellationToken)
  {
    var items = await _getAdminUsersHandler.Handle(
      new AdminUserListFilter(search, role, profileType, status),
      cancellationToken);

    return Ok(items);
  }

  [HttpGet("{userId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<AdminUserDetail>> GetById(
    Guid userId,
    CancellationToken cancellationToken)
  {
    var detail = await _getAdminUserDetailHandler.Handle(userId, cancellationToken);

    if (detail is null)
    {
      return this.ProblemResponse(
        StatusCodes.Status404NotFound,
        "Usuario no encontrado",
        UserFacingMessageTranslator.Translate($"User with ID {userId} not found."));
    }

    return Ok(detail);
  }

  [HttpPut("{userId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<AdminUserDetail>> Update(
    Guid userId,
    [FromBody] UpdateAdminUserRequest request,
    CancellationToken cancellationToken)
  {
    var detail = await _adminUserManagementService.UpdateIdentityAsync(
      userId,
      new AdminUserIdentityUpdate(
        request.Name,
        request.LastName,
        request.IdentificationNumber,
        request.Phone,
        request.Email),
      cancellationToken);

    return Ok(detail);
  }

  [HttpPut("{userId:guid}/roles")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<AdminUserDetail>> UpdateRoles(
    Guid userId,
    [FromBody] UpdateAdminUserRolesRequest request,
    CancellationToken cancellationToken)
  {
    var detail = await _adminUserManagementService.UpdateRolesAsync(
      userId,
      request.RoleNames,
      ResolveActorUserId(),
      cancellationToken);

    return Ok(detail);
  }

  [HttpPut("{userId:guid}/active-state")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<AdminUserDetail>> UpdateActiveState(
    Guid userId,
    [FromBody] UpdateAdminUserActiveStateRequest request,
    CancellationToken cancellationToken)
  {
    var detail = await _adminUserManagementService.UpdateActiveStateAsync(
      userId,
      request.IsActive,
      ResolveActorUserId(),
      cancellationToken);

    return Ok(detail);
  }

  [HttpPost("{userId:guid}/invalidate-sessions")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<AdminUserSessionInvalidationResult>> InvalidateSessions(
    Guid userId,
    CancellationToken cancellationToken)
  {
    var result = await _adminUserManagementService.InvalidateSessionsAsync(userId, cancellationToken);
    return Ok(result);
  }

  private Guid? ResolveActorUserId()
  {
    var rawValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawValue, out var userId) ? userId : null;
  }
}

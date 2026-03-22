using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Clients;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/clients")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminClientsController : ControllerBase
{
  private readonly GetAdminClientsHandler _getAdminClientsHandler;
  private readonly GetAdminClientDetailHandler _getAdminClientDetailHandler;
  private readonly IAdminClientManagementService _adminClientManagementService;

  public AdminClientsController(
    GetAdminClientsHandler getAdminClientsHandler,
    GetAdminClientDetailHandler getAdminClientDetailHandler,
    IAdminClientManagementService adminClientManagementService)
  {
    _getAdminClientsHandler = getAdminClientsHandler;
    _getAdminClientDetailHandler = getAdminClientDetailHandler;
    _adminClientManagementService = adminClientManagementService;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<IReadOnlyList<AdminClientListItem>>> Get(
    [FromQuery] string? search,
    [FromQuery] string? status,
    CancellationToken cancellationToken)
  {
    var items = await _getAdminClientsHandler.Handle(
      new AdminClientListFilter(search, status),
      cancellationToken);

    return Ok(items);
  }

  [HttpGet("{userId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AdminClientDetail>> GetById(
    Guid userId,
    CancellationToken cancellationToken)
  {
    var detail = await _getAdminClientDetailHandler.Handle(userId, cancellationToken);
    if (detail is null)
    {
      return this.ProblemResponse(
        StatusCodes.Status404NotFound,
        "Cliente no encontrado",
        "No se encontro el cliente solicitado.");
    }

    return Ok(detail);
  }

  [HttpPost]
  [ProducesResponseType(typeof(AdminClientDetail), StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> Create(
    [FromBody] AdminCreateClientRequest request,
    CancellationToken cancellationToken)
  {
    var actorUserId = ResolveActorUserId();
    if (actorUserId is null)
    {
      return AdminActorProblem();
    }

    var response = await _adminClientManagementService.CreateClientAsync(
      request,
      actorUserId.Value,
      cancellationToken);

    return CreatedAtAction(nameof(GetById), new { userId = response.UserId }, response);
  }

  [HttpPut("{userId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> Update(
    Guid userId,
    [FromBody] AdminUpdateClientRequest request,
    CancellationToken cancellationToken)
  {
    var actorUserId = ResolveActorUserId();
    if (actorUserId is null)
    {
      return AdminActorProblem();
    }

    var response = await _adminClientManagementService.UpdateClientAsync(
      userId,
      request,
      actorUserId.Value,
      cancellationToken);

    return Ok(response);
  }

  [HttpPut("{userId:guid}/active-state")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> UpdateActiveState(
    Guid userId,
    [FromBody] AdminSetClientActiveStateRequest request,
    CancellationToken cancellationToken)
  {
    var actorUserId = ResolveActorUserId();
    if (actorUserId is null)
    {
      return AdminActorProblem();
    }

    var response = await _adminClientManagementService.UpdateClientActiveStateAsync(
      userId,
      request,
      actorUserId.Value,
      cancellationToken);

    return Ok(response);
  }

  private IActionResult AdminActorProblem()
  {
    return this.ProblemResponse(
      StatusCodes.Status401Unauthorized,
      "No autorizado",
      "La sesion actual no incluye un identificador administrativo valido.");
  }

  private Guid? ResolveActorUserId()
  {
    var rawValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawValue, out var userId) ? userId : null;
  }
}

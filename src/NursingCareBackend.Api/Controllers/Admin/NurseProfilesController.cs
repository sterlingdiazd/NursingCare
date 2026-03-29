using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Services;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/nurse-profiles")]
[Authorize(Roles = "ADMIN")]
public sealed class NurseProfilesController : ControllerBase
{
    private readonly INurseProfileAdministrationService _nurseProfileAdministrationService;

    public NurseProfilesController(INurseProfileAdministrationService nurseProfileAdministrationService)
    {
        _nurseProfileAdministrationService = nurseProfileAdministrationService;
    }

    [HttpGet("pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var response = await _nurseProfileAdministrationService.GetPendingNurseProfilesAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var response = await _nurseProfileAdministrationService.GetActiveNurseProfilesAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("inactive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInactive(CancellationToken cancellationToken)
    {
        var response = await _nurseProfileAdministrationService.GetInactiveNurseProfilesAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var response = await _nurseProfileAdministrationService.GetNurseProfileAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(NursingCareBackend.Application.Identity.Responses.NurseProfileAdminResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] AdminCreateNurseProfileRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId();
        if (actorUserId is null)
        {
            return AdminActorProblem();
        }

        var response = await _nurseProfileAdministrationService.CreateNurseProfileAsync(
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
        [FromBody] AdminUpdateNurseProfileRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId();
        if (actorUserId is null)
        {
            return AdminActorProblem();
        }

        var response = await _nurseProfileAdministrationService.UpdateNurseProfileAsync(
            userId,
            request,
            actorUserId.Value,
            cancellationToken);

        return Ok(response);
    }

    [HttpPut("{userId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Complete(
        Guid userId,
        [FromBody] AdminCompleteNurseProfileRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId();
        if (actorUserId is null)
        {
            return AdminActorProblem();
        }

        var response = await _nurseProfileAdministrationService.CompleteNurseProfileCreationAsync(
            userId,
            request,
            actorUserId.Value,
            cancellationToken);

        return Ok(response);
    }

    [HttpPut("{userId:guid}/operational-access")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetOperationalAccess(
        Guid userId,
        [FromBody] AdminSetNurseOperationalAccessRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = ResolveActorUserId();
        if (actorUserId is null)
        {
            return AdminActorProblem();
        }

        var response = await _nurseProfileAdministrationService.SetOperationalAccessAsync(
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

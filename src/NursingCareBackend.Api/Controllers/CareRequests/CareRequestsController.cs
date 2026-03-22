using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.CareRequests;

[ApiController]
[Route("api/care-requests")]
public sealed class CareRequestsController : ControllerBase
{
    private readonly AssignCareRequestNurseHandler _assignNurseHandler;
    private readonly CreateCareRequestHandler _createHandler;
    private readonly TransitionCareRequestHandler _transitionHandler;
    private readonly GetCareRequestsHandler _getAllHandler;
    private readonly GetCareRequestByIdHandler _getByIdHandler;

    public CareRequestsController(
        AssignCareRequestNurseHandler assignNurseHandler,
        CreateCareRequestHandler createHandler,
        TransitionCareRequestHandler transitionHandler,
        GetCareRequestsHandler getAllHandler,
        GetCareRequestByIdHandler getByIdHandler)
    {
        _assignNurseHandler = assignNurseHandler;
        _createHandler = createHandler;
        _transitionHandler = transitionHandler;
        _getAllHandler = getAllHandler;
        _getByIdHandler = getByIdHandler;
    }

    [HttpPost]
    [Authorize(Policy = "CareRequestCreator")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCareRequestRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return this.ProblemResponse(
                StatusCodes.Status401Unauthorized,
                "No autorizado",
                "La sesion actual no incluye un identificador de usuario valido.");
        }

        var command = new CreateCareRequestCommand
        {
            UserID = userId,
            Description = request.CareRequestDescription,
            CareRequestReason = null,
            CareRequestType = request.CareRequestType,
            SuggestedNurse = request.SuggestedNurse,
            AssignedNurse = null,
            Unit = request.Unit,
            Price = request.Price,
            ClientBasePriceOverride = request.ClientBasePriceOverride,
            DistanceFactor = request.DistanceFactor,
            ComplexityLevel = request.ComplexityLevel,
            MedicalSuppliesCost = request.MedicalSuppliesCost,
            CareRequestDate = request.CareRequestDate
        };

        var id = await _createHandler.Handle(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet]
    [Authorize(Policy = "CareRequestReader")]
    public async Task<ActionResult<IReadOnlyList<CareRequestResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!TryResolveAccessScope(out var accessScope))
        {
            return this.ProblemResponse(
                StatusCodes.Status401Unauthorized,
                "No autorizado",
                "La sesion actual no incluye un identificador de usuario valido.");
        }

        var careRequests = await _getAllHandler.Handle(accessScope, cancellationToken);
        var response = careRequests
            .Select(CareRequestResponse.FromDomain)
            .ToList()
            .AsReadOnly();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CareRequestReader")]
    public async Task<ActionResult<CareRequestResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryResolveAccessScope(out var accessScope))
        {
            return this.ProblemResponse(
                StatusCodes.Status401Unauthorized,
                "No autorizado",
                "La sesion actual no incluye un identificador de usuario valido.");
        }

        var careRequest = await _getByIdHandler.Handle(id, accessScope, cancellationToken);

        if (careRequest is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                "Solicitud no encontrada",
                "No se encontro la solicitud.");
        }

        return Ok(CareRequestResponse.FromDomain(careRequest));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "CareRequestApprover")]
    public Task<ActionResult<CareRequestResponse>> Approve(Guid id, CancellationToken cancellationToken)
      => Transition(id, CareRequestTransitionAction.Approve, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "CareRequestApprover")]
    public Task<ActionResult<CareRequestResponse>> Reject(Guid id, CancellationToken cancellationToken)
      => Transition(id, CareRequestTransitionAction.Reject, cancellationToken);

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "CareRequestCompleter")]
    public Task<ActionResult<CareRequestResponse>> Complete(Guid id, CancellationToken cancellationToken)
      => Transition(id, CareRequestTransitionAction.Complete, cancellationToken);

    [HttpPut("{id:guid}/assignment")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<CareRequestResponse>> AssignNurse(
        Guid id,
        [FromBody] AssignCareRequestNurseRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _assignNurseHandler.Handle(
            new AssignCareRequestNurseCommand(id, request.AssignedNurse),
            cancellationToken);

        return Ok(CareRequestResponse.FromDomain(updated));
    }

    private async Task<ActionResult<CareRequestResponse>> Transition(
        Guid id,
        CareRequestTransitionAction action,
        CancellationToken cancellationToken)
    {
        var updated = await _transitionHandler.Handle(
            new TransitionCareRequestCommand(id, action, ResolveCurrentUserId()),
            cancellationToken);

        return Ok(CareRequestResponse.FromDomain(updated));
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private bool TryResolveAccessScope(out CareRequestAccessScope accessScope)
    {
        if (User.IsInRole(SystemRoles.Admin))
        {
            accessScope = CareRequestAccessScope.Admin;
            return true;
        }

        var userId = ResolveCurrentUserId();
        if (!userId.HasValue)
        {
            accessScope = CareRequestAccessScope.Admin;
            return false;
        }

        if (User.IsInRole(SystemRoles.Nurse))
        {
            accessScope = CareRequestAccessScope.ForNurse(userId.Value);
            return true;
        }

        accessScope = CareRequestAccessScope.ForClient(userId.Value);
        return true;
    }
}

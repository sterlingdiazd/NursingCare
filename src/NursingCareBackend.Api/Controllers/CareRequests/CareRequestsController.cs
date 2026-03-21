using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;
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
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authenticated user identifier is missing",
                Detail = "The current session does not include a valid user identifier.",
                Instance = HttpContext.Request.Path
            });
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
        var careRequests = await _getAllHandler.Handle(ResolveAccessScope(), cancellationToken);
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
        var careRequest = await _getByIdHandler.Handle(id, ResolveAccessScope(), cancellationToken);

        if (careRequest is null)
        {
            return NotFound();
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
        try
        {
            var updated = await _assignNurseHandler.Handle(
                new AssignCareRequestNurseCommand(id, request.AssignedNurse),
                cancellationToken);

            return Ok(CareRequestResponse.FromDomain(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Care request assignment failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Care request assignment failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }

    private async Task<ActionResult<CareRequestResponse>> Transition(
        Guid id,
        CareRequestTransitionAction action,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _transitionHandler.Handle(
                new TransitionCareRequestCommand(id, action, ResolveCurrentUserId()),
                cancellationToken);

            return Ok(CareRequestResponse.FromDomain(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Care request transition failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private CareRequestAccessScope ResolveAccessScope()
    {
        if (User.IsInRole(SystemRoles.Admin))
        {
            return CareRequestAccessScope.Admin;
        }

        var userId = ResolveCurrentUserId();
        if (!userId.HasValue)
        {
            return CareRequestAccessScope.Admin;
        }

        if (User.IsInRole(SystemRoles.Nurse))
        {
            return CareRequestAccessScope.ForNurse(userId.Value);
        }

        return CareRequestAccessScope.ForClient(userId.Value);
    }
}

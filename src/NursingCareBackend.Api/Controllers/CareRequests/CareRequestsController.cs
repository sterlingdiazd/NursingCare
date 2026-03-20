using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;

namespace NursingCareBackend.Api.Controllers.CareRequests;

[ApiController]
[Route("api/care-requests")]
public sealed class CareRequestsController : ControllerBase
{
    private readonly CreateCareRequestHandler _createHandler;
    private readonly TransitionCareRequestHandler _transitionHandler;
    private readonly GetCareRequestsHandler _getAllHandler;
    private readonly GetCareRequestByIdHandler _getByIdHandler;

    public CareRequestsController(
        CreateCareRequestHandler createHandler,
        TransitionCareRequestHandler transitionHandler,
        GetCareRequestsHandler getAllHandler,
        GetCareRequestByIdHandler getByIdHandler)
    {
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
            NurseId = request.NurseId,
            SuggestedNurse = request.SuggestedNurse,
            AssignedNurse = request.AssignedNurse,
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
        var careRequests = await _getAllHandler.Handle(cancellationToken);
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
        var careRequest = await _getByIdHandler.Handle(id, cancellationToken);

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

    private async Task<ActionResult<CareRequestResponse>> Transition(
        Guid id,
        CareRequestTransitionAction action,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _transitionHandler.Handle(
                new TransitionCareRequestCommand(id, action),
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
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;

namespace NursingCareBackend.Api.Controllers.CareRequests;

[ApiController]
[Authorize(Policy = "CareRequestWriter")]
[Route("api/care-requests")]
public sealed class CareRequestsController : ControllerBase
{
    private readonly CreateCareRequestHandler _createHandler;
    private readonly GetCareRequestsHandler _getAllHandler;
    private readonly GetCareRequestByIdHandler _getByIdHandler;

    public CareRequestsController(
        CreateCareRequestHandler createHandler,
        GetCareRequestsHandler getAllHandler,
        GetCareRequestByIdHandler getByIdHandler)
    {
        _createHandler = createHandler;
        _getAllHandler = getAllHandler;
        _getByIdHandler = getByIdHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCareRequestRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCareRequestCommand
        {
            ResidentId = request.ResidentId,
            Description = request.Description
        };

        var id = await _createHandler.Handle(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet]
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
}
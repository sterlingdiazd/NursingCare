using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;

namespace NursingCareBackend.Api.Controllers.CareRequests;

[ApiController]
[Route("api/care-requests")]
public sealed class CareRequestsController : ControllerBase
{
    private readonly CreateCareRequestHandler _handler;

    public CareRequestsController(CreateCareRequestHandler handler)
    {
        _handler = handler;
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

        var id = await _handler.Handle(command, cancellationToken);

        return CreatedAtAction(nameof(Create), new { id }, new { id });
    }
}
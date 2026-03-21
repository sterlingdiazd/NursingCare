using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Services;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/nurse-profiles")]
[Authorize(Roles = "Admin")]
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

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _nurseProfileAdministrationService.GetNurseProfileAsync(userId, cancellationToken);
            return Ok(response);
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
                Title = "Nurse profile retrieval failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
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
        try
        {
            var response = await _nurseProfileAdministrationService.CompleteNurseProfileCreationAsync(
                userId,
                request,
                cancellationToken);

            return Ok(response);
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
                Title = "Nurse profile completion failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nurse profile completion failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }
}

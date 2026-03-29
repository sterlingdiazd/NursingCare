using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.AdminPortal.Settings;
using NursingCareBackend.Application.Identity.Authentication;
using System.Security.Claims;

namespace NursingCareBackend.Api.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[ApiController]
[Route("api/admin/settings")]
public sealed class AdminSettingsController : ControllerBase
{
    private readonly IAdminSettingsManagementService _settings;

    public AdminSettingsController(IAdminSettingsManagementService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> List(CancellationToken ct)
    {
        var result = await _settings.ListSettingsAsync(ct);
        return Ok(result);
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<SystemSettingDto>> Get(string key, CancellationToken ct)
    {
        try
        {
            var result = await _settings.GetSettingAsync(key, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Detail = ex.Message });
        }
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<SystemSettingDto>> Update(string key, UpdateSystemSettingRequest request, CancellationToken ct)
    {
        var actorUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(actorUserIdStr, out var actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _settings.UpdateSettingAsync(key, request, actorUserId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Detail = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails { Detail = ex.Message });
        }
    }
}

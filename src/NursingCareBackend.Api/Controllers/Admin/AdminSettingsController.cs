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
    private readonly ILogger<AdminSettingsController> _logger;

    public AdminSettingsController(IAdminSettingsManagementService settings, ILogger<AdminSettingsController> logger)
    {
        _settings = settings;
        _logger = logger;
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
            _logger.LogWarning(ex, "Setting not found: {Key}", key);
            return NotFound(new ProblemDetails { Detail = "La configuración solicitada no fue encontrada." });
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
            _logger.LogWarning(ex, "Setting not found for update: {Key}", key);
            return NotFound(new ProblemDetails { Detail = "La configuración solicitada no fue encontrada." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized setting update attempt: {Key}", key);
            return Unauthorized(new ProblemDetails { Detail = "No tiene permisos para modificar esta configuración." });
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.Notifications;

namespace NursingCareBackend.Api.Controllers.Notifications;

/// <summary>
/// Per-device push token registration. Any authenticated user (admin / nurse /
/// client) may call. The mobile app calls POST on every login + cold start;
/// DELETE on explicit logout.
/// </summary>
[ApiController]
[Route("api/notifications/push-tokens")]
[Authorize]
public sealed class NotificationPushTokensController : ControllerBase
{
  private readonly IPushTokenService _service;

  public NotificationPushTokensController(IPushTokenService service)
  {
    _service = service;
  }

  public sealed class RegisterPushTokenBody
  {
    public string ExpoPushToken { get; set; } = string.Empty;
    public string Platform { get; set; } = "unknown";
    public string? DeviceId { get; set; }
    public string? AppVersion { get; set; }
    public string? Locale { get; set; }
  }

  [HttpPost]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Register(
    [FromBody] RegisterPushTokenBody body,
    CancellationToken cancellationToken)
  {
    var userId = ResolveUserId();
    if (userId is null) return Unauthorized();
    if (string.IsNullOrWhiteSpace(body.ExpoPushToken))
    {
      return BadRequest(new { error = "ExpoPushToken is required." });
    }

    await _service.RegisterAsync(
      userId.Value,
      new RegisterPushTokenRequest(
        ExpoPushToken: body.ExpoPushToken,
        Platform: body.Platform,
        DeviceId: body.DeviceId,
        AppVersion: body.AppVersion,
        Locale: body.Locale),
      cancellationToken);
    return NoContent();
  }

  [HttpDelete("{deviceId}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Deactivate(string deviceId, CancellationToken cancellationToken)
  {
    var userId = ResolveUserId();
    if (userId is null) return Unauthorized();
    await _service.DeactivateForDeviceAsync(userId.Value, deviceId, cancellationToken);
    return NoContent();
  }

  private Guid? ResolveUserId()
  {
    var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(raw, out var id) ? id : null;
  }
}

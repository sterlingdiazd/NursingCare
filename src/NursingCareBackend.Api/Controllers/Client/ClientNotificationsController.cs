using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Client;

[ApiController]
[Route("api/client/notifications")]
[Authorize(Roles = SystemRoles.Client)]
public sealed class ClientNotificationsController : ControllerBase
{
  private readonly IUserNotificationService _service;

  public ClientNotificationsController(IUserNotificationService service)
  {
    _service = service;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<UserNotificationListPage>> List(
    [FromQuery] UserNotificationStatus status = UserNotificationStatus.Active,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = UserNotificationListFilter.DefaultPageSize,
    CancellationToken cancellationToken = default)
  {
    var userId = ResolveActorUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    var filter = UserNotificationListFilter.Sanitized(status, page, pageSize);
    var pageResult = await _service.ListForUserAsync(userId.Value, filter, cancellationToken);
    return Ok(pageResult);
  }

  [HttpGet("summary")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<UserNotificationSummary>> GetSummary(CancellationToken cancellationToken = default)
  {
    var userId = ResolveActorUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    var summary = await _service.GetSummaryAsync(userId.Value, cancellationToken);
    return Ok(summary);
  }

  [HttpPost("{id:guid}/read")]
  public Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    => UpdateOwnedNotification(id, (userId, notificationId, ct) =>
      _service.MarkAsReadAsync(userId, notificationId, ct), cancellationToken);

  [HttpPost("{id:guid}/unread")]
  public Task<IActionResult> MarkAsUnread(Guid id, CancellationToken cancellationToken = default)
    => UpdateOwnedNotification(id, (userId, notificationId, ct) =>
      _service.MarkAsUnreadAsync(userId, notificationId, ct), cancellationToken);

  [HttpPost("{id:guid}/archive")]
  public Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
    => UpdateOwnedNotification(id, (userId, notificationId, ct) =>
      _service.ArchiveAsync(userId, notificationId, ct), cancellationToken);

  [HttpPost("{id:guid}/dismiss")]
  public Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken = default)
    => UpdateOwnedNotification(id, (userId, notificationId, ct) =>
      _service.DismissAsync(userId, notificationId, ct), cancellationToken);

  private async Task<IActionResult> UpdateOwnedNotification(
    Guid id,
    Func<Guid, Guid, CancellationToken, Task> update,
    CancellationToken cancellationToken)
  {
    var userId = ResolveActorUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    try
    {
      await update(userId.Value, id, cancellationToken);
      return NoContent();
    }
    catch (KeyNotFoundException)
    {
      return NotFound();
    }
  }

  private Guid? ResolveActorUserId()
  {
    var rawValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawValue, out var userId) ? userId : null;
  }
}

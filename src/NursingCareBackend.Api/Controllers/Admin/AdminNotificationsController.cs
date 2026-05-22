using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminNotificationsController : ControllerBase
{
  private readonly IAdminNotificationService _service;

  public AdminNotificationsController(IAdminNotificationService service)
  {
    _service = service;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AdminNotificationListPage>> List(
    [FromQuery] AdminNotificationStatus status = AdminNotificationStatus.Active,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = AdminNotificationListFilter.DefaultPageSize,
    CancellationToken cancellationToken = default)
  {
    var adminUserId = ResolveActorUserId();
    if (adminUserId is null)
    {
      return Unauthorized();
    }

    var filter = AdminNotificationListFilter.Sanitized(status, page, pageSize);
    var pageResult = await _service.ListForAdminAsync(adminUserId.Value, filter, cancellationToken);
    return Ok(pageResult);
  }

  [HttpGet("summary")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AdminNotificationSummary>> GetSummary(CancellationToken cancellationToken = default)
  {
    var adminUserId = ResolveActorUserId();
    if (adminUserId is null)
    {
      return Unauthorized();
    }

    var summary = await _service.GetSummaryAsync(adminUserId.Value, cancellationToken);
    return Ok(summary);
  }

  [HttpPost("{id:guid}/read")]
  public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
  {
    var adminUserId = ResolveActorUserId();
    if (adminUserId is null)
    {
      return Unauthorized();
    }

    try
    {
      await _service.MarkAsReadAsync(adminUserId.Value, id, cancellationToken);
      return NoContent();
    }
    catch (KeyNotFoundException)
    {
      return NotFound();
    }
  }

  [HttpPost("{id:guid}/unread")]
  public async Task<IActionResult> MarkAsUnread(Guid id, CancellationToken cancellationToken = default)
  {
    var adminUserId = ResolveActorUserId();
    if (adminUserId is null)
    {
      return Unauthorized();
    }

    try
    {
      await _service.MarkAsUnreadAsync(adminUserId.Value, id, cancellationToken);
      return NoContent();
    }
    catch (KeyNotFoundException)
    {
      return NotFound();
    }
  }

  [HttpPost("{id:guid}/archive")]
  public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
  {
    var adminUserId = ResolveActorUserId();
    if (adminUserId is null)
    {
      return Unauthorized();
    }

    try
    {
      await _service.ArchiveAsync(adminUserId.Value, id, cancellationToken);
      return NoContent();
    }
    catch (KeyNotFoundException)
    {
      return NotFound();
    }
  }

  [HttpPost("{id:guid}/dismiss")]
  public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken = default)
  {
    var adminUserId = ResolveActorUserId();
    if (adminUserId is null)
    {
      return Unauthorized();
    }

    try
    {
      await _service.DismissAsync(adminUserId.Value, id, cancellationToken);
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

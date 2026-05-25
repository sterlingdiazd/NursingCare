using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Localization;
using NursingCareBackend.Application.Identity.ClientProfiles;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Client;

[ApiController]
[Route("api/client/profile")]
[Authorize(Roles = SystemRoles.Client)]
public sealed class ClientProfileController : ControllerBase
{
  private readonly IClientSelfProfileService _profileService;

  public ClientProfileController(IClientSelfProfileService profileService)
  {
    _profileService = profileService;
  }

  [HttpGet]
  [ProducesResponseType(typeof(ClientSelfProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Get(CancellationToken cancellationToken)
  {
    var userId = ResolveCurrentUserId();
    if (userId is null)
    {
      return UnauthorizedProblem();
    }

    try
    {
      var profile = await _profileService.GetAsync(userId.Value, cancellationToken);
      if (profile is null)
      {
        return this.ProblemResponse(
          StatusCodes.Status404NotFound,
          "Perfil no encontrado",
          "No encontramos el perfil de cliente de esta cuenta.");
      }

      return Ok(profile);
    }
    catch (UnauthorizedAccessException)
    {
      return this.ProblemResponse(
        StatusCodes.Status403Forbidden,
        Messages.Get("errors.acceso_denegado"),
        Messages.Get("errors.acceso_denegado_detalle"));
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(
        StatusCodes.Status409Conflict,
        "Perfil no disponible",
        ex.Message);
    }
  }

  [HttpPut]
  [ProducesResponseType(typeof(ClientSelfProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Update(
    [FromBody] UpdateClientSelfProfileRequest request,
    CancellationToken cancellationToken)
  {
    var userId = ResolveCurrentUserId();
    if (userId is null)
    {
      return UnauthorizedProblem();
    }

    try
    {
      var updated = await _profileService.UpdateAsync(userId.Value, request, cancellationToken);
      return Ok(updated);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(
        StatusCodes.Status404NotFound,
        "Perfil no encontrado",
        "No encontramos el perfil de cliente de esta cuenta.");
    }
    catch (UnauthorizedAccessException)
    {
      return this.ProblemResponse(
        StatusCodes.Status403Forbidden,
        Messages.Get("errors.acceso_denegado"),
        Messages.Get("errors.acceso_denegado_detalle"));
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(
        StatusCodes.Status409Conflict,
        "Perfil no disponible",
        ex.Message);
    }
    catch (ArgumentException ex)
    {
      return this.ProblemResponse(
        StatusCodes.Status400BadRequest,
        Messages.Get("errors.datos_invalidos"),
        ex.Message);
    }
  }

  private Guid? ResolveCurrentUserId()
  {
    var rawValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawValue, out var userId) ? userId : null;
  }

  private ObjectResult UnauthorizedProblem()
    => this.ProblemResponse(
      StatusCodes.Status401Unauthorized,
      Messages.Get("errors.no_autorizado"),
      Messages.Get("errors.sesion_sin_usuario"));
}

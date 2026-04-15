using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Application.Identity.Services;

namespace NursingCareBackend.Api.Controllers.Catalog;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogOptionsController : ControllerBase
{
    private readonly ICatalogOptionsService _catalogOptions;
    private readonly INurseProfileAdministrationService _nurseProfileAdministration;

    public CatalogOptionsController(
        ICatalogOptionsService catalogOptions,
        INurseProfileAdministrationService nurseProfileAdministration)
    {
        _catalogOptions = catalogOptions;
        _nurseProfileAdministration = nurseProfileAdministration;
    }

    [HttpGet("care-request-options")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CatalogOptionsResponse>> GetCareRequestOptions(CancellationToken cancellationToken)
    {
        var options = await _catalogOptions.GetCareRequestOptionsAsync(cancellationToken);
        return Ok(options);
    }

    /// <summary>
    /// Lectura publica de especialidades y categorias activas para registro y formularios sin sesion.
    /// </summary>
    [HttpGet("nurse-profile-options")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NurseProfileOptionsResponse>> GetNurseProfileOptions(CancellationToken cancellationToken)
    {
        var options = await _catalogOptions.GetNurseProfileOptionsAsync(cancellationToken);
        return Ok(options);
    }

    [HttpGet("available-nurses")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AvailableNurseOptionResponse>>> GetAvailableNurses(
        CancellationToken cancellationToken)
    {
        var nurses = await _nurseProfileAdministration.GetActiveNurseProfilesAsync(cancellationToken);
        var response = nurses
            .Where(nurse => nurse.IsAssignmentReady)
            .Select(nurse => new AvailableNurseOptionResponse(
                nurse.UserId,
                BuildDisplayName(nurse.Name, nurse.LastName, nurse.Email),
                nurse.Specialty ?? string.Empty,
                nurse.Category ?? string.Empty))
            .OrderBy(nurse => nurse.DisplayName)
            .ToArray();

        return Ok(response);
    }

    private static string BuildDisplayName(string? name, string? lastName, string email)
    {
        var fullName = $"{name ?? string.Empty} {lastName ?? string.Empty}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? email : fullName;
    }

    public sealed record AvailableNurseOptionResponse(
        Guid UserId,
        string DisplayName,
        string Specialty,
        string Category);
}

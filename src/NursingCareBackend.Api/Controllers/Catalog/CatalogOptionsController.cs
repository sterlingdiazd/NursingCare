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
        var activeNurses = await _nurseProfileAdministration.GetActiveNurseProfilesAsync(cancellationToken);

        var response = new List<AvailableNurseOptionResponse>();
        foreach (var summary in activeNurses.Where(n => n.IsAssignmentReady))
        {
            var displayName = BuildDisplayName(summary.Name, summary.LastName, summary.Email);
            response.Add(new AvailableNurseOptionResponse(
                summary.UserId,
                displayName,
                summary.Specialty ?? string.Empty,
                summary.Category ?? string.Empty));
        }

        return Ok(response.OrderBy(n => n.DisplayName).ToArray());
    }

    private static string BuildDisplayName(string? name, string? lastName, string email)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(lastName))
        {
            return $"{name.Trim()} {lastName.Trim()}";
        }
        
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }
        
        return email;
    }

    public sealed record AvailableNurseOptionResponse(
        Guid UserId,
        string DisplayName,
        string Specialty,
        string Category);
}

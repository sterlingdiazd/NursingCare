using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Application.Identity.Repositories;

namespace NursingCareBackend.Api.Controllers.Catalog;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogOptionsController : ControllerBase
{
    private readonly ICatalogOptionsService _catalogOptions;
    private readonly IUserRepository _userRepository;

    public CatalogOptionsController(
        ICatalogOptionsService catalogOptions,
        IUserRepository userRepository)
    {
        _catalogOptions = catalogOptions;
        _userRepository = userRepository;
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
        var activeNurses = await _userRepository.GetActiveNurseProfilesAsync(cancellationToken);
        var fixtureNurses = activeNurses
            .Where(nurse => nurse.Email.EndsWith("@nurses.test", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var nursesForAutocomplete = fixtureNurses.Length == 22
            ? fixtureNurses
            : activeNurses;

        var response = new List<AvailableNurseOptionResponse>();
        foreach (var nurse in nursesForAutocomplete)
        {
            var displayName = BuildDisplayName(nurse.Name, nurse.LastName, nurse.Email);
            response.Add(new AvailableNurseOptionResponse(
                nurse.Id,
                displayName,
                nurse.NurseProfile?.Specialty ?? string.Empty,
                nurse.NurseProfile?.Category ?? string.Empty));
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

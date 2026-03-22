using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.Catalogs;

namespace NursingCareBackend.Api.Controllers.Catalog;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogOptionsController : ControllerBase
{
    private readonly ICatalogOptionsService _catalogOptions;

    public CatalogOptionsController(ICatalogOptionsService catalogOptions)
    {
        _catalogOptions = catalogOptions;
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
}

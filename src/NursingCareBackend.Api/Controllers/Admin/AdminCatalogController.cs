using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Catalog;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/catalog")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminCatalogController : ControllerBase
{
    private readonly IAdminCatalogManagementService _catalog;

    public AdminCatalogController(IAdminCatalogManagementService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("care-request-categories")]
    public async Task<ActionResult<IReadOnlyList<CareRequestCategoryListItemDto>>> ListCareRequestCategories(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListCareRequestCategoriesAsync(includeInactive, cancellationToken));

    [HttpPut("care-request-categories/{id:guid}")]
    public async Task<IActionResult> UpdateCareRequestCategory(
        Guid id,
        [FromBody] UpdateCareRequestCategoryRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateCareRequestCategoryAsync(
                id,
                body.DisplayName,
                body.CategoryFactor,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("care-request-categories")]
    public async Task<ActionResult<Guid>> CreateCareRequestCategory(
        [FromBody] CreateCareRequestCategoryRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _catalog.CreateCareRequestCategoryAsync(
                body.Code,
                body.DisplayName,
                body.CategoryFactor,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpGet("care-request-types")]
    public async Task<ActionResult<IReadOnlyList<CareRequestTypeListItemDto>>> ListCareRequestTypes(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListCareRequestTypesAsync(includeInactive, cancellationToken));

    [HttpPut("care-request-types/{id:guid}")]
    public async Task<IActionResult> UpdateCareRequestType(
        Guid id,
        [FromBody] UpdateCareRequestTypeRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateCareRequestTypeAsync(
                id,
                body.DisplayName,
                body.CareRequestCategoryCode,
                body.UnitTypeCode,
                body.BasePrice,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpPost("care-request-types")]
    public async Task<ActionResult<Guid>> CreateCareRequestType(
        [FromBody] CreateCareRequestTypeRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _catalog.CreateCareRequestTypeAsync(
                body.Code,
                body.DisplayName,
                body.CareRequestCategoryCode,
                body.UnitTypeCode,
                body.BasePrice,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpGet("unit-types")]
    public async Task<ActionResult<IReadOnlyList<UnitTypeListItemDto>>> ListUnitTypes(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListUnitTypesAsync(includeInactive, cancellationToken));

    [HttpPut("unit-types/{id:guid}")]
    public async Task<IActionResult> UpdateUnitType(
        Guid id,
        [FromBody] UpdateUnitTypeRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateUnitTypeAsync(id, body.DisplayName, body.IsActive, body.DisplayOrder, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("unit-types")]
    public async Task<ActionResult<Guid>> CreateUnitType(
        [FromBody] CreateUnitTypeRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _catalog.CreateUnitTypeAsync(
                body.Code,
                body.DisplayName,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpGet("distance-factors")]
    public async Task<ActionResult<IReadOnlyList<DistanceFactorListItemDto>>> ListDistanceFactors(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListDistanceFactorsAsync(includeInactive, cancellationToken));

    [HttpPut("distance-factors/{id:guid}")]
    public async Task<IActionResult> UpdateDistanceFactor(
        Guid id,
        [FromBody] UpdateDistanceFactorRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateDistanceFactorAsync(
                id,
                body.DisplayName,
                body.Multiplier,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("distance-factors")]
    public async Task<ActionResult<Guid>> CreateDistanceFactor(
        [FromBody] CreateDistanceFactorRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _catalog.CreateDistanceFactorAsync(
                body.Code,
                body.DisplayName,
                body.Multiplier,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpGet("complexity-levels")]
    public async Task<ActionResult<IReadOnlyList<ComplexityLevelListItemDto>>> ListComplexityLevels(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListComplexityLevelsAsync(includeInactive, cancellationToken));

    [HttpPut("complexity-levels/{id:guid}")]
    public async Task<IActionResult> UpdateComplexityLevel(
        Guid id,
        [FromBody] UpdateComplexityLevelRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateComplexityLevelAsync(
                id,
                body.DisplayName,
                body.Multiplier,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("complexity-levels")]
    public async Task<ActionResult<Guid>> CreateComplexityLevel(
        [FromBody] CreateComplexityLevelRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _catalog.CreateComplexityLevelAsync(
                body.Code,
                body.DisplayName,
                body.Multiplier,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpGet("volume-discount-rules")]
    public async Task<ActionResult<IReadOnlyList<VolumeDiscountRuleListItemDto>>> ListVolumeDiscountRules(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListVolumeDiscountRulesAsync(includeInactive, cancellationToken));

    [HttpPut("volume-discount-rules/{id:guid}")]
    public async Task<IActionResult> UpdateVolumeDiscountRule(
        Guid id,
        [FromBody] UpdateVolumeDiscountRuleRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateVolumeDiscountRuleAsync(
                id,
                body.MinimumCount,
                body.DiscountPercent,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("volume-discount-rules")]
    public async Task<ActionResult<Guid>> CreateVolumeDiscountRule(
        [FromBody] CreateVolumeDiscountRuleRequest body,
        CancellationToken cancellationToken)
    {
        var id = await _catalog.CreateVolumeDiscountRuleAsync(
            body.MinimumCount,
            body.DiscountPercent,
            body.IsActive,
            body.DisplayOrder,
            cancellationToken);
        return Ok(id);
    }

    [HttpGet("nurse-specialties")]
    public async Task<ActionResult<IReadOnlyList<NurseSpecialtyListItemDto>>> ListNurseSpecialties(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListNurseSpecialtiesAsync(includeInactive, cancellationToken));

    [HttpPut("nurse-specialties/{id:guid}")]
    public async Task<IActionResult> UpdateNurseSpecialty(
        Guid id,
        [FromBody] UpdateNurseSpecialtyRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateNurseSpecialtyAsync(
                id,
                body.DisplayName,
                body.AlternativeCodes,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("nurse-specialties")]
    public async Task<ActionResult<Guid>> CreateNurseSpecialty(
        [FromBody] CreateNurseSpecialtyRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _catalog.CreateNurseSpecialtyAsync(
                body.Code,
                body.DisplayName,
                body.AlternativeCodes,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpGet("nurse-categories")]
    public async Task<ActionResult<IReadOnlyList<NurseCategoryListItemDto>>> ListNurseCategories(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _catalog.ListNurseCategoriesAsync(includeInactive, cancellationToken));

    [HttpPut("nurse-categories/{id:guid}")]
    public async Task<IActionResult> UpdateNurseCategory(
        Guid id,
        [FromBody] UpdateNurseCategoryRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalog.UpdateNurseCategoryAsync(
                id,
                body.DisplayName,
                body.AlternativeCodes,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("nurse-categories")]
    public async Task<ActionResult<Guid>> CreateNurseCategory(
        [FromBody] CreateNurseCategoryRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _catalog.CreateNurseCategoryAsync(
                body.Code,
                body.DisplayName,
                body.AlternativeCodes,
                body.IsActive,
                body.DisplayOrder,
                cancellationToken);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
    }

    [HttpPost("pricing-preview")]
    public async Task<ActionResult<PricingPreviewResponse>> PreviewPricing(
        [FromBody] PricingPreviewRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _catalog.PreviewPricingAsync(body, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Solicitud invalida.", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Calculo invalido.", ex.Message);
        }
    }

}

public sealed class UpdateCareRequestCategoryRequest
{
    public string DisplayName { get; set; } = default!;
    public decimal CategoryFactor { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateCareRequestCategoryRequest
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public decimal CategoryFactor { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateCareRequestTypeRequest
{
    public string DisplayName { get; set; } = default!;
    public string CareRequestCategoryCode { get; set; } = default!;
    public string UnitTypeCode { get; set; } = default!;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateCareRequestTypeRequest
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string CareRequestCategoryCode { get; set; } = default!;
    public string UnitTypeCode { get; set; } = default!;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateUnitTypeRequest
{
    public string DisplayName { get; set; } = default!;
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateUnitTypeRequest
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateDistanceFactorRequest
{
    public string DisplayName { get; set; } = default!;
    public decimal Multiplier { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateDistanceFactorRequest
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public decimal Multiplier { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateComplexityLevelRequest
{
    public string DisplayName { get; set; } = default!;
    public decimal Multiplier { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateComplexityLevelRequest
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public decimal Multiplier { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateVolumeDiscountRuleRequest
{
    public int MinimumCount { get; set; }
    public int DiscountPercent { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateVolumeDiscountRuleRequest
{
    public int MinimumCount { get; set; }
    public int DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateNurseSpecialtyRequest
{
    public string DisplayName { get; set; } = default!;
    public string? AlternativeCodes { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateNurseSpecialtyRequest
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? AlternativeCodes { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateNurseCategoryRequest
{
    public string DisplayName { get; set; } = default!;
    public string? AlternativeCodes { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CreateNurseCategoryRequest
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? AlternativeCodes { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

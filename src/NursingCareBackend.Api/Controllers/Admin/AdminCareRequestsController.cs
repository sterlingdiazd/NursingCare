using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/care-requests")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminCareRequestsController : ControllerBase
{
  private readonly GetAdminCareRequestsHandler _getListHandler;
  private readonly GetAdminCareRequestDetailHandler _getDetailHandler;
  private readonly GetAdminCareRequestClientOptionsHandler _getClientOptionsHandler;
  private readonly CreateCareRequestHandler _createHandler;
  private readonly IUserRepository _userRepository;

  public AdminCareRequestsController(
    GetAdminCareRequestsHandler getListHandler,
    GetAdminCareRequestDetailHandler getDetailHandler,
    GetAdminCareRequestClientOptionsHandler getClientOptionsHandler,
    CreateCareRequestHandler createHandler,
    IUserRepository userRepository)
  {
    _getListHandler = getListHandler;
    _getDetailHandler = getDetailHandler;
    _getClientOptionsHandler = getClientOptionsHandler;
    _createHandler = createHandler;
    _userRepository = userRepository;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<IReadOnlyList<AdminCareRequestListItem>>> Get(
    [FromQuery] string? view,
    [FromQuery] string? search,
    [FromQuery] DateOnly? scheduledFrom,
    [FromQuery] DateOnly? scheduledTo,
    [FromQuery] string? sort,
    CancellationToken cancellationToken)
  {
    var items = await _getListHandler.Handle(
      new AdminCareRequestListFilter(view, search, scheduledFrom, scheduledTo, sort),
      cancellationToken);

    return Ok(items);
  }

  [HttpGet("clients")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<IReadOnlyList<AdminCareRequestClientOption>>> GetClients(
    [FromQuery] string? search,
    CancellationToken cancellationToken)
  {
    var items = await _getClientOptionsHandler.Handle(search, cancellationToken);
    return Ok(items);
  }

  [HttpGet("export")]
  [Produces("text/csv")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<IActionResult> Export(
    [FromQuery] string? view,
    [FromQuery] string? search,
    [FromQuery] DateOnly? scheduledFrom,
    [FromQuery] DateOnly? scheduledTo,
    [FromQuery] string? sort,
    CancellationToken cancellationToken)
  {
    var items = await _getListHandler.Handle(
      new AdminCareRequestListFilter(view, search, scheduledFrom, scheduledTo, sort),
      cancellationToken);

    var csv = new StringBuilder();
    csv.AppendLine("Id,Estado,Cliente,CorreoCliente,Enfermera,CorreoEnfermera,Tipo,FechaServicio,Total,Creada,Actualizada");

    foreach (var item in items)
    {
      csv.AppendLine(string.Join(",",
        EscapeCsv(item.Id),
        EscapeCsv(item.Status),
        EscapeCsv(item.ClientDisplayName),
        EscapeCsv(item.ClientEmail),
        EscapeCsv(item.AssignedNurseDisplayName),
        EscapeCsv(item.AssignedNurseEmail),
        EscapeCsv(item.CareRequestType),
        EscapeCsv(item.CareRequestDate?.ToString("yyyy-MM-dd")),
        EscapeCsv(item.Total),
        EscapeCsv(item.CreatedAtUtc),
        EscapeCsv(item.UpdatedAtUtc)));
    }

    var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    var fileName = $"solicitudes-admin-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
    return File(bytes, "text/csv; charset=utf-8", fileName);
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<AdminCareRequestDetail>> GetById(
    Guid id,
    CancellationToken cancellationToken)
  {
    var detail = await _getDetailHandler.Handle(id, cancellationToken);

    if (detail is null)
    {
      return this.ProblemResponse(
        StatusCodes.Status404NotFound,
        "Solicitud no encontrada",
        "No se encontro la solicitud administrativa.");
    }

    return Ok(detail);
  }

  [HttpPost]
  [ProducesResponseType(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Create(
    [FromBody] CreateAdminCareRequestRequest request,
    CancellationToken cancellationToken)
  {
    var clientUser = await _userRepository.GetByIdAsync(request.ClientUserId, cancellationToken);

    if (clientUser is null
      || clientUser.ProfileType != UserProfileType.Client
      || !clientUser.IsActive
      || clientUser.ClientProfile is null)
    {
      return this.ProblemResponse(
        StatusCodes.Status400BadRequest,
        "Cliente invalido",
        "Debes seleccionar un cliente activo y valido para crear la solicitud.");
    }

    var id = await _createHandler.Handle(
      new CreateCareRequestCommand
      {
        UserID = request.ClientUserId,
        Description = request.CareRequestDescription,
        CareRequestReason = null,
        CareRequestType = request.CareRequestType,
        SuggestedNurse = request.SuggestedNurse,
        AssignedNurse = null,
        Unit = request.Unit,
        Price = request.Price,
        ClientBasePriceOverride = request.ClientBasePriceOverride,
        DistanceFactor = request.DistanceFactor,
        ComplexityLevel = request.ComplexityLevel,
        MedicalSuppliesCost = request.MedicalSuppliesCost,
        CareRequestDate = request.CareRequestDate,
      },
      cancellationToken);

    return CreatedAtAction(nameof(GetById), new { id }, new { id });
  }

  private static string EscapeCsv(object? value)
  {
    if (value is null)
    {
      return "\"\"";
    }

    var text = value.ToString() ?? string.Empty;
    return $"\"{text.Replace("\"", "\"\"")}\"";
  }
}

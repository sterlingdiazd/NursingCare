using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Application.AdminPortal.Shifts;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
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
    private readonly RegisterCareRequestShiftHandler _registerShiftHandler;
    private readonly RecordCareRequestShiftChangeHandler _recordShiftChangeHandler;
    private readonly TransitionCareRequestHandler _transitionHandler;

    public AdminCareRequestsController(
      GetAdminCareRequestsHandler getListHandler,
      GetAdminCareRequestDetailHandler getDetailHandler,
      GetAdminCareRequestClientOptionsHandler getClientOptionsHandler,
      CreateCareRequestHandler createHandler,
      IUserRepository userRepository,
      RegisterCareRequestShiftHandler registerShiftHandler,
      RecordCareRequestShiftChangeHandler recordShiftChangeHandler,
      TransitionCareRequestHandler transitionHandler)
    {
        _getListHandler = getListHandler;
        _getDetailHandler = getDetailHandler;
        _getClientOptionsHandler = getClientOptionsHandler;
        _createHandler = createHandler;
        _userRepository = userRepository;
        _registerShiftHandler = registerShiftHandler;
        _recordShiftChangeHandler = recordShiftChangeHandler;
        _transitionHandler = transitionHandler;
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
          || clientUser.ProfileType != UserProfileType.CLIENT
          || !clientUser.IsActive
          || clientUser.ClientProfile is null
          || !clientUser.UserRoles.Any(userRole => string.Equals(
            userRole.Role.Name,
            SystemRoles.Client,
            StringComparison.OrdinalIgnoreCase)))
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

    [HttpPost("{id:guid}/shifts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterShift(
      Guid id,
      [FromBody] RegisterAdminCareRequestShiftRequest request,
      CancellationToken cancellationToken)
    {
        var shiftId = await _registerShiftHandler.Handle(
          new RegisterCareRequestShiftCommand(
            id,
            request.NurseUserId,
            request.ScheduledStartUtc,
            request.ScheduledEndUtc),
          cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, new { shiftId });
    }

    [HttpPost("{id:guid}/shifts/{shiftId:guid}/changes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordShiftChange(
      Guid id,
      Guid shiftId,
      [FromBody] RecordAdminCareRequestShiftChangeRequest request,
      CancellationToken cancellationToken)
    {
        await _recordShiftChangeHandler.Handle(
          new RecordCareRequestShiftChangeCommand(
            id,
            shiftId,
            request.NewNurseUserId,
            request.Reason ?? string.Empty,
            request.EffectiveAtUtc),
          cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/invoice")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Invoice(
      Guid id,
      [FromBody] InvoiceCareRequestRequest request,
      CancellationToken cancellationToken)
    {
        try
        {
            var careRequest = await _transitionHandler.Handle(
              new TransitionCareRequestCommand(
                id,
                CareRequestTransitionAction.Invoice,
                InvoiceNumber: request.InvoiceNumber),
              cancellationToken);

            return Ok(new InvoicedCareRequestResponse(
              careRequest.Id,
              careRequest.InvoiceNumber!,
              careRequest.InvoicedAtUtc!.Value,
              careRequest.Total));
        }
        catch (KeyNotFoundException)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", "No se encontró la solicitud.");
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Operación inválida", ex.Message);
        }
    }

    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pay(
      Guid id,
      [FromBody] PayCareRequestRequest request,
      CancellationToken cancellationToken)
    {
        try
        {
            var careRequest = await _transitionHandler.Handle(
              new TransitionCareRequestCommand(
                id,
                CareRequestTransitionAction.Pay,
                BankReference: request.BankReference),
              cancellationToken);

            return Ok(new PaidCareRequestResponse(
              careRequest.Id,
              careRequest.PaidAtUtc!.Value,
              careRequest.Total));
        }
        catch (KeyNotFoundException)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", "No se encontró la solicitud.");
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Operación inválida", ex.Message);
        }
    }

    [HttpPost("{id:guid}/void")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Void(
      Guid id,
      [FromBody] VoidCareRequestRequest request,
      CancellationToken cancellationToken)
    {
        try
        {
            var careRequest = await _transitionHandler.Handle(
              new TransitionCareRequestCommand(
                id,
                CareRequestTransitionAction.Void,
                Reason: request.VoidReason),
              cancellationToken);

            return Ok(new VoidedCareRequestResponse(
              careRequest.Id,
              careRequest.VoidedAtUtc!.Value,
              careRequest.VoidReason!));
        }
        catch (KeyNotFoundException)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", "No se encontró la solicitud.");
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Operación inválida", ex.Message);
        }
    }

    [HttpPost("{id:guid}/receipt")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateReceipt(
      Guid id,
      CancellationToken cancellationToken)
    {
        try
        {
            var careRequest = await _transitionHandler.Handle(
              new TransitionCareRequestCommand(id, CareRequestTransitionAction.GenerateReceipt),
              cancellationToken);

            return Ok(new GenerateReceiptResponse(
              careRequest.Id.ToString(),
              careRequest.ReceiptNumber!,
              null));
        }
        catch (KeyNotFoundException)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", "No se encontró la solicitud.");
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Operación inválida", ex.Message);
        }
    }

    [HttpGet("{id:guid}/receipt")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipt(
      Guid id,
      CancellationToken cancellationToken)
    {
        var detail = await _getDetailHandler.Handle(id, cancellationToken);

        if (detail is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", "No se encontró la solicitud.");
        }

        if (string.IsNullOrEmpty(detail.ReceiptNumber))
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Recibo no encontrado", "Esta solicitud no tiene recibo generado.");
        }

        return Ok(new GetReceiptResponse(
          id.ToString(),
          detail.ReceiptNumber,
          null,
          detail.ReceiptGeneratedAtUtc!.Value));
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

public sealed class RegisterAdminCareRequestShiftRequest
{
    public Guid? NurseUserId { get; set; }

    public DateTime? ScheduledStartUtc { get; set; }

    public DateTime? ScheduledEndUtc { get; set; }
}

public sealed class RecordAdminCareRequestShiftChangeRequest
{
    public Guid? NewNurseUserId { get; set; }

    public string? Reason { get; set; }

    public DateTime? EffectiveAtUtc { get; set; }
}

public sealed record InvoiceCareRequestRequest(
    [Required][StringLength(50)] string InvoiceNumber
);

public sealed record PayCareRequestRequest(
    [Required][StringLength(100)] string BankReference
);

public sealed record VoidCareRequestRequest(
    [Required][StringLength(500)] string VoidReason
);

public sealed record InvoicedCareRequestResponse(Guid Id, string InvoiceNumber, DateTime InvoicedAtUtc, decimal TotalAmount);
public sealed record PaidCareRequestResponse(Guid Id, DateTime PaidAtUtc, decimal TotalAmount);
public sealed record VoidedCareRequestResponse(Guid Id, DateTime VoidedAtUtc, string VoidReason);
public sealed record GenerateReceiptResponse(string ReceiptId, string ReceiptNumber, string? ReceiptContentBase64);
public sealed record GetReceiptResponse(string ReceiptId, string ReceiptNumber, string? ReceiptContentBase64, DateTime GeneratedAtUtc);

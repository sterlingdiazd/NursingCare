using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Api.Localization;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Application.AdminPortal.Shifts;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CompleteByAdmin;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;
using NursingCareBackend.Application.CareRequests.Commands.InvoiceCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.IssueCreditNote;
using NursingCareBackend.Application.CareRequests.Commands.PayCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.RejectPaymentProof;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.VoidCareRequest;
using NursingCareBackend.Application.CareRequests.Queries.GetPaymentClaim;
using NursingCareBackend.Application.CareRequests.Queries.GetReceipt;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.CareRequests;
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
  private readonly InvoiceCareRequestHandler _invoiceHandler;
  private readonly PayCareRequestHandler _payHandler;
  private readonly VoidCareRequestHandler _voidHandler;
  private readonly RejectPaymentProofHandler _rejectPaymentHandler;
  private readonly IssueCreditNoteHandler _issueCreditNoteHandler;
  private readonly GenerateReceiptHandler _generateReceiptHandler;
  private readonly GetReceiptHandler _getReceiptHandler;
  private readonly GetPaymentClaimHandler _getPaymentClaimHandler;
  private readonly IPaymentProofRepository _paymentProofRepository;
  private readonly CompleteByAdminHandler _completeByAdminHandler;
  private readonly TransitionCareRequestHandler _transitionHandler;
  private readonly AssignCareRequestNurseHandler _assignNurseHandler;

  public AdminCareRequestsController(
    GetAdminCareRequestsHandler getListHandler,
    GetAdminCareRequestDetailHandler getDetailHandler,
    GetAdminCareRequestClientOptionsHandler getClientOptionsHandler,
    CreateCareRequestHandler createHandler,
    IUserRepository userRepository,
    RegisterCareRequestShiftHandler registerShiftHandler,
    RecordCareRequestShiftChangeHandler recordShiftChangeHandler,
    InvoiceCareRequestHandler invoiceHandler,
    PayCareRequestHandler payHandler,
    VoidCareRequestHandler voidHandler,
    RejectPaymentProofHandler rejectPaymentHandler,
    IssueCreditNoteHandler issueCreditNoteHandler,
    GenerateReceiptHandler generateReceiptHandler,
    GetReceiptHandler getReceiptHandler,
    GetPaymentClaimHandler getPaymentClaimHandler,
    IPaymentProofRepository paymentProofRepository,
    CompleteByAdminHandler completeByAdminHandler,
    TransitionCareRequestHandler transitionHandler,
    AssignCareRequestNurseHandler assignNurseHandler)
  {
    _getListHandler = getListHandler;
    _getDetailHandler = getDetailHandler;
    _getClientOptionsHandler = getClientOptionsHandler;
    _createHandler = createHandler;
    _userRepository = userRepository;
    _registerShiftHandler = registerShiftHandler;
    _recordShiftChangeHandler = recordShiftChangeHandler;
    _invoiceHandler = invoiceHandler;
    _payHandler = payHandler;
    _voidHandler = voidHandler;
    _rejectPaymentHandler = rejectPaymentHandler;
    _issueCreditNoteHandler = issueCreditNoteHandler;
    _generateReceiptHandler = generateReceiptHandler;
    _getReceiptHandler = getReceiptHandler;
    _getPaymentClaimHandler = getPaymentClaimHandler;
    _paymentProofRepository = paymentProofRepository;
    _completeByAdminHandler = completeByAdminHandler;
    _transitionHandler = transitionHandler;
    _assignNurseHandler = assignNurseHandler;
  }

  // Returns the payment-proof image the client uploaded, for the admin to verify before paying.
  [HttpGet("{id:guid}/payment-proof")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetPaymentProof(Guid id, CancellationToken cancellationToken)
  {
    var proof = await _paymentProofRepository.GetLatestByCareRequestIdAsync(id, cancellationToken);
    if (proof is null)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Comprobante no encontrado",
        $"No hay comprobante de pago para la solicitud '{id}'.");
    }

    return File(proof.Content, proof.ContentType);
  }

  // Structured payment claim + anti-fraud flags (reused bank reference, amount mismatch) for the
  // admin to review before confirming. The image is fetched separately via /payment-proof.
  [HttpGet("{id:guid}/payment-claim")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetPaymentClaim(Guid id, CancellationToken cancellationToken)
  {
    var result = await _getPaymentClaimHandler.Handle(id, cancellationToken);
    if (result is null)
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);

    return Ok(result);
  }

  private Guid GetAdminUserId()
  {
    var claim = User.FindFirst(ClaimTypes.NameIdentifier);
    return claim != null ? Guid.Parse(claim.Value) : Guid.Empty;
  }

  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<ActionResult<AdminCareRequestListPage>> Get(
    [FromQuery] string? view,
    [FromQuery] string? search,
    [FromQuery] DateOnly? scheduledFrom,
    [FromQuery] DateOnly? scheduledTo,
    [FromQuery] string? sort,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = AdminCareRequestListFilter.DefaultPageSize,
    CancellationToken cancellationToken = default)
  {
    var filter = AdminCareRequestListFilter.Sanitized(view, search, scheduledFrom, scheduledTo, sort, page, pageSize);
    var result = await _getListHandler.Handle(filter, cancellationToken);
    return Ok(result);
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
    // Export returns all matching rows — use page 1 with int.MaxValue to bypass pagination.
    var exportFilter = new AdminCareRequestListFilter(view, search, scheduledFrom, scheduledTo, sort, 1, int.MaxValue);
    var page = await _getListHandler.Handle(exportFilter, cancellationToken);
    var items = page.Items;

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
        Messages.Get("errors.solicitud_no_encontrada"),
        Messages.Get("errors.solicitud_admin_no_encontrada_detalle"));
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
        Messages.Get("errors.cliente_invalido"),
        Messages.Get("errors.cliente_invalido_detalle"));
    }

    // Admin-created requests must always have an assigned nurse.
    if (request.AssignedNurseId == Guid.Empty)
    {
      return this.ProblemResponse(
        StatusCodes.Status400BadRequest,
        "Enfermera requerida",
        "Las solicitudes creadas por el administrador deben tener una enfermera asignada.");
    }

    var nurseUser = await _userRepository.GetByIdAsync(request.AssignedNurseId, cancellationToken);
    if (nurseUser is null
      || nurseUser.ProfileType != UserProfileType.NURSE
      || nurseUser.NurseProfile is null
      || !nurseUser.IsActive
      || !nurseUser.NurseProfile.IsActive)
    {
      return this.ProblemResponse(
        StatusCodes.Status400BadRequest,
        "Enfermera no válida",
        "La enfermera seleccionada no existe, no tiene perfil activo o no está habilitada para operar.");
    }

    if (!nurseUser.UserRoles.Any(userRole => string.Equals(
          userRole.Role.Name,
          SystemRoles.Nurse,
          StringComparison.OrdinalIgnoreCase)))
    {
      return this.ProblemResponse(
        StatusCodes.Status400BadRequest,
        "Enfermera no válida",
        "El usuario seleccionado no tiene el rol de enfermera.");
    }

    var id = await _createHandler.Handle(
      new CreateCareRequestCommand
      {
        UserID = request.ClientUserId,
        Description = request.CareRequestDescription,
        CareRequestReason = null,
        CareRequestType = request.CareRequestType,
        SuggestedNurse = request.SuggestedNurse,
        AssignedNurse = request.AssignedNurseId,
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

  /// <summary>
  /// Approves a Pending care request on behalf of the admin.
  /// The request must already have an assigned nurse.
  /// </summary>
  [HttpPost("{id:guid}/approve")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
  {
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    try
    {
      var result = await _transitionHandler.Handle(
        new TransitionCareRequestCommand(id, CareRequestTransitionAction.Approve, adminId, null),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
    }
  }

  /// <summary>
  /// Rejects a Pending care request.
  /// </summary>
  [HttpPost("{id:guid}/reject")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Reject(
    Guid id,
    [FromBody] AdminRejectCareRequestRequest? request,
    CancellationToken cancellationToken)
  {
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    try
    {
      var result = await _transitionHandler.Handle(
        new TransitionCareRequestCommand(id, CareRequestTransitionAction.Reject, adminId, request?.Reason),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
    }
  }

  /// <summary>
  /// Assigns or reassigns a nurse to a care request.
  /// </summary>
  [HttpPut("{id:guid}/assignment")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> AssignNurse(
    Guid id,
    [FromBody] AdminAssignNurseRequest request,
    CancellationToken cancellationToken)
  {
    try
    {
      var result = await _assignNurseHandler.Handle(
        new AssignCareRequestNurseCommand(id, request.AssignedNurse),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
    }
  }

  /// <summary>
  /// Completes an Approved care request on the admin's authority, using the assigned nurse as the actor.
  /// </summary>
  [HttpPost("{id:guid}/complete")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
  {
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    try
    {
      var result = await _completeByAdminHandler.Handle(
        new CompleteByAdminCommand(id, adminId),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
    }
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

  [HttpPost("{id:guid}/invoice")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Invoice(
    Guid id,
    [FromBody] InvoiceCareRequestRequest request,
    CancellationToken cancellationToken)
  {
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    try
    {
      var result = await _invoiceHandler.Handle(
        new InvoiceCareRequestCommand(id, request.InvoiceNumber, request.InvoiceDate ?? DateTime.UtcNow, adminId),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
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
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    try
    {
      var result = await _payHandler.Handle(
        new PayCareRequestCommand(id, request.BankReference, request.PaymentDate ?? DateTime.UtcNow, adminId,
          request.AcknowledgeDuplicateReference),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
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
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    try
    {
      var result = await _voidHandler.Handle(
        new VoidCareRequestCommand(id, request.VoidReason, adminId, DateTime.UtcNow),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
    }
  }

  // POST /api/admin/care-requests/{id}/reject-payment
  // Admin rejects a reported payment proof (with a required reason): the request returns to
  // Invoiced, the proof is cleared, and the client is notified to re-report. Audited.
  [HttpPost("{id:guid}/reject-payment")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RejectPayment(
    Guid id,
    [FromBody] RejectPaymentRequest request,
    CancellationToken cancellationToken)
  {
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    var reason = request?.Reason?.Trim();
    if (string.IsNullOrWhiteSpace(reason))
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.razon_requerida"), "El motivo es requerido.");

    try
    {
      var result = await _rejectPaymentHandler.Handle(
        new RejectPaymentProofCommand(id, adminId, reason),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
    }
  }

  // POST /api/admin/care-requests/{id}/credit-note
  // Admin records a credit note / refund against an already-Paid request. The request stays Paid
  // (Void is blocked after Paid by design); this writes an auditable in-books ledger entry so a
  // reversed/refunded payment is no longer invisible. The domain caps total credits at the amount
  // paid. Audited; the client is notified.
  [HttpPost("{id:guid}/credit-note")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> IssueCreditNote(
    Guid id,
    [FromBody] IssueCreditNoteRequest request,
    CancellationToken cancellationToken)
  {
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    var reason = request?.Reason?.Trim();
    if (string.IsNullOrWhiteSpace(reason))
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.razon_requerida"), "El motivo es requerido.");

    if (request!.Amount <= 0m)
      return this.ProblemResponse(StatusCodes.Status400BadRequest, "Monto inválido", "El monto debe ser mayor que cero.");

    try
    {
      var result = await _issueCreditNoteHandler.Handle(
        new IssueCreditNoteCommand(id, request.Amount, reason, request.Reference?.Trim(), adminId),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.transicion_invalida"), ex.Message);
    }
    catch (ArgumentException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, "Monto inválido", ex.Message);
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
    var adminId = GetAdminUserId();
    if (adminId == Guid.Empty)
      return Unauthorized();

    try
    {
      var result = await _generateReceiptHandler.Handle(
        new GenerateReceiptCommand(id, adminId),
        cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException)
    {
      return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada", null);
    }
    catch (InvalidOperationException ex)
    {
      return this.ProblemResponse(StatusCodes.Status400BadRequest, Messages.Get("errors.estado_invalido"), ex.Message);
    }
  }

  [HttpGet("{id:guid}/receipt")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetReceipt(
    Guid id,
    CancellationToken cancellationToken)
  {
    var result = await _getReceiptHandler.Handle(id, cancellationToken);

    if (result is null)
      return this.ProblemResponse(StatusCodes.Status404NotFound, Messages.Get("errors.recibo_no_encontrado"), $"No existe recibo para la solicitud '{id}'.");

    return Ok(result);
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

public sealed class InvoiceCareRequestRequest
{
  [Required]
  [MaxLength(50)]
  public string InvoiceNumber { get; set; } = default!;
  public DateTime? InvoiceDate { get; set; }
}

public sealed class PayCareRequestRequest
{
  [Required]
  [MaxLength(100)]
  public string BankReference { get; set; } = default!;
  public DateTime? PaymentDate { get; set; }
  // Anti-fraud: set true to override the "bank reference already used" guard (rare legit reuse).
  public bool AcknowledgeDuplicateReference { get; set; }
}

public sealed class VoidCareRequestRequest
{
  [Required]
  [MaxLength(500)]
  public string VoidReason { get; set; } = default!;
}

public sealed class RejectPaymentRequest
{
  [Required]
  [MaxLength(500)]
  public string Reason { get; set; } = default!;
}

public sealed class IssueCreditNoteRequest
{
  [Range(0.01, double.MaxValue)]
  public decimal Amount { get; set; }

  [Required]
  [MaxLength(500)]
  public string Reason { get; set; } = default!;

  [MaxLength(100)]
  public string? Reference { get; set; }
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

public sealed class AdminRejectCareRequestRequest
{
  [MaxLength(500)]
  public string? Reason { get; set; }
}

public sealed class AdminAssignNurseRequest
{
  [Required]
  public Guid AssignedNurse { get; set; }
}

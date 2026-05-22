using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NursingCareBackend.Api.Localization;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.ReportPayment;
using NursingCareBackend.Application.CareRequests.Queries;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.CareRequests;

[ApiController]
[Route("api/care-requests")]
public sealed class CareRequestsController : ControllerBase
{
    private readonly AssignCareRequestNurseHandler _assignNurseHandler;
    private readonly CreateCareRequestHandler _createHandler;
    private readonly TransitionCareRequestHandler _transitionHandler;
    private readonly GetCareRequestsHandler _getAllHandler;
    private readonly GetCareRequestByIdHandler _getByIdHandler;
    private readonly VerifyPricingHandler _verifyPricingHandler;
    private readonly ReportPaymentHandler _reportPaymentHandler;
    private readonly IUserRepository _userRepository;

    public CareRequestsController(
        AssignCareRequestNurseHandler assignNurseHandler,
        CreateCareRequestHandler createHandler,
        TransitionCareRequestHandler transitionHandler,
        GetCareRequestsHandler getAllHandler,
        GetCareRequestByIdHandler getByIdHandler,
        VerifyPricingHandler verifyPricingHandler,
        ReportPaymentHandler reportPaymentHandler,
        IUserRepository userRepository)
    {
        _assignNurseHandler = assignNurseHandler;
        _createHandler = createHandler;
        _transitionHandler = transitionHandler;
        _getAllHandler = getAllHandler;
        _getByIdHandler = getByIdHandler;
        _verifyPricingHandler = verifyPricingHandler;
        _reportPaymentHandler = reportPaymentHandler;
        _userRepository = userRepository;
    }

    [HttpPost]
    [Authorize(Policy = "CareRequestCreator")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCareRequestRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var callerUserId))
        {
            return this.ProblemResponse(
                StatusCodes.Status401Unauthorized,
                Messages.Get("errors.no_autorizado"),
                Messages.Get("errors.sesion_sin_usuario"));
        }

        var callerIsAdmin = User.IsInRole(SystemRoles.Admin);

        // Resolve the client (the user the care request is FOR).
        // - Admin caller: must specify ClientUserId in the body. The selected
        //   user must exist and have ProfileType=CLIENT.
        // - Client caller: ClientUserId is ignored; the JWT subject is the client.
        Guid clientUserId;
        if (callerIsAdmin)
        {
            if (request.ClientUserId is null || request.ClientUserId.Value == Guid.Empty)
            {
                return this.ProblemResponse(
                    StatusCodes.Status400BadRequest,
                    Messages.Get("errors.cliente_requerido"),
                    Messages.Get("errors.cliente_requerido_detalle"));
            }

            var targetClient = await _userRepository.GetByIdAsync(request.ClientUserId.Value, cancellationToken);
            if (targetClient is null
                || targetClient.ProfileType != UserProfileType.CLIENT
                || !targetClient.UserRoles.Any(ur => ur.Role.Name == SystemRoles.Client))
            {
                return this.ProblemResponse(
                    StatusCodes.Status400BadRequest,
                    Messages.Get("errors.cliente_no_valido"),
                    Messages.Get("errors.cliente_no_valido_detalle"));
            }

            clientUserId = targetClient.Id;
        }
        else
        {
            // Non-admin caller cannot impersonate another user.
            if (request.ClientUserId.HasValue && request.ClientUserId.Value != callerUserId)
            {
                return this.ProblemResponse(
                    StatusCodes.Status403Forbidden,
                    Messages.Get("errors.acceso_denegado"),
                    Messages.Get("errors.acceso_denegado_detalle"));
            }
            clientUserId = callerUserId;
        }

        var command = new CreateCareRequestCommand
        {
            UserID = clientUserId,
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
            CareRequestDate = request.CareRequestDate
        };

        var id = await _createHandler.Handle(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet]
    [Authorize(Policy = "CareRequestReader")]
    public async Task<ActionResult<IReadOnlyList<CareRequestResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!TryResolveAccessScope(out var accessScope))
        {
            return this.ProblemResponse(
                StatusCodes.Status401Unauthorized,
                Messages.Get("errors.no_autorizado"),
                Messages.Get("errors.sesion_sin_usuario"));
        }

        var careRequests = await _getAllHandler.Handle(accessScope, cancellationToken);
        var response = careRequests
            .Select(CareRequestResponse.FromDomain)
            .ToList()
            .AsReadOnly();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CareRequestReader")]
    public async Task<ActionResult<CareRequestResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryResolveAccessScope(out var accessScope))
        {
            return this.ProblemResponse(
                StatusCodes.Status401Unauthorized,
                Messages.Get("errors.no_autorizado"),
                Messages.Get("errors.sesion_sin_usuario"));
        }

        var careRequest = await _getByIdHandler.Handle(id, accessScope, cancellationToken);

        if (careRequest is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                Messages.Get("errors.solicitud_no_encontrada"),
                Messages.Get("errors.solicitud_no_encontrada_detalle"));
        }

        return Ok(CareRequestResponse.FromDomain(careRequest));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "CareRequestApprover")]
    public Task<ActionResult<CareRequestResponse>> Approve(Guid id, CancellationToken cancellationToken)
      => Transition(id, CareRequestTransitionAction.Approve, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "CareRequestApprover")]
    public Task<ActionResult<CareRequestResponse>> Reject(
        Guid id,
        [FromBody] RejectCareRequestRequest? request,
        CancellationToken cancellationToken)
      => Transition(id, CareRequestTransitionAction.Reject, cancellationToken, reason: request?.Reason);

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "CareRequestCompleter")]
    public Task<ActionResult<CareRequestResponse>> Complete(Guid id, CancellationToken cancellationToken)
      => Transition(id, CareRequestTransitionAction.Complete, cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "CareRequestCanceller")]
    public Task<ActionResult<CareRequestResponse>> Cancel(Guid id, CancellationToken cancellationToken)
      => Transition(id, CareRequestTransitionAction.Cancel, cancellationToken);

    // Client uploads a payment proof (invoice photo / transfer screenshot) and reports the payment.
    // Ownership is enforced by the client access scope inside the handler.
    [HttpPost("{id:guid}/report-payment")]
    [Authorize(Policy = "CareRequestReader")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> ReportPayment(
        Guid id,
        [FromForm] ReportPaymentForm form,
        CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var proof = form.Proof;
        var note = form.Note;
        if (proof is null || proof.Length == 0)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Comprobante requerido",
                "Debes adjuntar una imagen del comprobante de pago.");
        }

        if (proof.Length > 5_000_000)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Archivo muy grande",
                "El comprobante no debe superar 5 MB.");
        }

        var contentType = (proof.ContentType ?? string.Empty).ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/jpg" or "image/png"))
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Formato no soportado",
                "El comprobante debe ser una imagen JPG o PNG.");
        }

        using var ms = new MemoryStream();
        await proof.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        // Validate the actual file signature (magic bytes), not just the client-declared MIME.
        if (!IsSupportedImage(bytes))
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Formato no soportado",
                "El comprobante debe ser una imagen JPG o PNG válida.");
        }

        try
        {
            var updated = await _reportPaymentHandler.Handle(
                new ReportPaymentCommand(id, userId.Value, bytes, contentType, note),
                cancellationToken);
            return Ok(CareRequestResponse.FromDomain(updated));
        }
        catch (KeyNotFoundException)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Solicitud no encontrada",
                $"No se encontró la solicitud '{id}'.");
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "Operación inválida", ex.Message);
        }
    }

    [HttpGet("{id:guid}/verify-pricing")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<PricingVerificationResponse>> VerifyPricing(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _verifyPricingHandler.HandleAsync(id, cancellationToken);

        if (result is null)
        {
            return this.ProblemResponse(
                StatusCodes.Status404NotFound,
                Messages.Get("errors.solicitud_no_encontrada"),
                Messages.Get("errors.solicitud_cuidado_no_encontrada_detalle"));
        }

        if (result.FieldComparisons.Count == 0 && !result.Matches)
        {
            return this.ProblemResponse(
                StatusCodes.Status422UnprocessableEntity,
                "Verificacion no disponible",
                "La verificacion de precios no esta disponible para esta solicitud.");
        }

        return Ok(PricingVerificationResponse.FromResult(result));
    }

    [HttpPut("{id:guid}/assignment")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<CareRequestResponse>> AssignNurse(
        Guid id,
        [FromBody] AssignCareRequestNurseRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _assignNurseHandler.Handle(
            new AssignCareRequestNurseCommand(id, request.AssignedNurse),
            cancellationToken);

        return Ok(CareRequestResponse.FromDomain(updated));
    }

    private async Task<ActionResult<CareRequestResponse>> Transition(
        Guid id,
        CareRequestTransitionAction action,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        var updated = await _transitionHandler.Handle(
            new TransitionCareRequestCommand(id, action, ResolveCurrentUserId(), reason),
            cancellationToken);

        return Ok(CareRequestResponse.FromDomain(updated));
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    // Verifies the file signature: JPEG (FF D8 FF) or PNG (89 50 4E 47 0D 0A 1A 0A).
    private static bool IsSupportedImage(byte[] bytes)
    {
        if (bytes.Length < 8) return false;
        var isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                    && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        return isJpeg || isPng;
    }

    private bool TryResolveAccessScope(out CareRequestAccessScope accessScope)
    {
        if (User.IsInRole(SystemRoles.Admin))
        {
            accessScope = CareRequestAccessScope.Admin;
            return true;
        }

        var userId = ResolveCurrentUserId();
        if (!userId.HasValue)
        {
            accessScope = CareRequestAccessScope.Admin;
            return false;
        }

        if (User.IsInRole(SystemRoles.Nurse))
        {
            accessScope = CareRequestAccessScope.ForNurse(userId.Value);
            return true;
        }

        accessScope = CareRequestAccessScope.ForClient(userId.Value);
        return true;
    }
}

/// <summary>Multipart form for reporting a payment. Bound as a single model so Swashbuckle can
/// describe the file upload in the OpenAPI document.</summary>
public sealed class ReportPaymentForm
{
    public IFormFile Proof { get; set; } = default!;
    public string? Note { get; set; }
}

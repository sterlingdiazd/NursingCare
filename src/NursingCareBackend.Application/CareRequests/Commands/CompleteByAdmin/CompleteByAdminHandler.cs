using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.CompleteByAdmin;

public sealed class CompleteByAdminHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IAdminNotificationPublisher _notifications;
    private readonly IPayrollCompensationService _payrollCompensationService;
    private readonly IAdminAuditService _auditService;
    private readonly IInvoiceNumberGenerator _invoiceNumbers;

    public CompleteByAdminHandler(
        ICareRequestRepository repository,
        IAdminNotificationPublisher notifications,
        IPayrollCompensationService payrollCompensationService,
        IAdminAuditService auditService,
        IInvoiceNumberGenerator invoiceNumbers)
    {
        _repository = repository;
        _notifications = notifications;
        _payrollCompensationService = payrollCompensationService;
        _auditService = auditService;
        _invoiceNumbers = invoiceNumbers;
    }

    public async Task<CareRequest> Handle(CompleteByAdminCommand command, CancellationToken cancellationToken)
    {
        var careRequest = await _repository.GetByIdAsync(
            command.CareRequestId,
            CareRequestAccessScope.Admin,
            cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        if (!careRequest.AssignedNurse.HasValue)
        {
            throw new InvalidOperationException("La solicitud no tiene una enfermera asignada. Asignar una enfermera antes de completar.");
        }

        var completedAtUtc = DateTime.UtcNow;

        // Admin uses the assigned nurse's ID as the actor so the domain rule is satisfied.
        careRequest.Complete(completedAtUtc, careRequest.AssignedNurse.Value);
        await _repository.UpdateAsync(careRequest, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.AdminUserId,
                ActorRole: "Admin",
                Action: "CompleteByAdmin",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: "Completado por el administrador en nombre de la enfermera asignada.",
                MetadataJson: null),
            cancellationToken);

        await _payrollCompensationService.RecordExecutionForCompletedCareRequestAsync(careRequest, cancellationToken);

        // Auto-generate the invoice immediately after completion (same behaviour as TransitionCareRequestHandler).
        var invoiceNumber = await _invoiceNumbers.NextAsync(completedAtUtc, cancellationToken);
        careRequest.Invoice(invoiceNumber, completedAtUtc);
        await _repository.UpdateAsync(careRequest, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.AdminUserId,
                ActorRole: "Admin",
                Action: "Invoice",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: $"Factura {invoiceNumber} generada automaticamente al completar el servicio por el administrador.",
                MetadataJson: null),
            cancellationToken);

        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "care_request_completed",
                Severity: "Medium",
                Title: "Solicitud completada por el administrador",
                Body: $"La solicitud \"{careRequest.Description}\" fue marcada como completada por el administrador.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Administracion",
                RequiresAction: false),
            cancellationToken);

        return careRequest;
    }
}

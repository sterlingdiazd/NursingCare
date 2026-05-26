using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.Exceptions;
using NursingCareBackend.Application.Notifications;

namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ReverseNursePayment;

public sealed class ReverseNursePaymentHandler
{
    private readonly INursePeriodPaymentRepository _paymentRepository;
    private readonly IAdminAuditService _auditService;
    private readonly IUserNotificationPublisher _userNotifications;
    private readonly ILogger<ReverseNursePaymentHandler> _logger;

    public ReverseNursePaymentHandler(
        INursePeriodPaymentRepository paymentRepository,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications,
        ILogger<ReverseNursePaymentHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _auditService = auditService;
        _userNotifications = userNotifications;
        _logger = logger;
    }

    public async Task<NursePaymentStateResult> Handle(
        ReverseNursePaymentCommand command,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetAsync(command.PeriodId, command.NurseUserId, cancellationToken);
        if (payment is null)
        {
            throw new VoucherNotFoundException(command.PeriodId, command.NurseUserId);
        }

        payment.Reverse(command.Reason, command.AdminUserId, DateTime.UtcNow);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.AdminUserId,
                ActorRole: "Admin",
                Action: AdminAuditActions.NursePaymentReversed,
                EntityType: "NursePeriodPayment",
                EntityId: payment.Id.ToString(),
                Notes: $"Pago revertido (enfermera {command.NurseUserId}, período {command.PeriodId}). Motivo: {command.Reason}",
                MetadataJson: null),
            cancellationToken);

        // Best-effort: tell the nurse her payment was reversed (transparency). Never fail on this.
        try
        {
            await _userNotifications.PublishToUserAsync(
                new UserNotificationPublishRequest(
                    RecipientUserId: command.NurseUserId,
                    Category: "nurse_payment_reversed",
                    Severity: "High",
                    Title: "Pago revertido",
                    Body: $"Tu pago de un período fue revertido. Motivo: {command.Reason}. Te avisaremos cuando se reprocese.",
                    EntityType: "NursePeriodPayment",
                    EntityId: payment.Id.ToString(),
                    DeepLinkPath: "/nurse/payroll",
                    Source: "Nómina",
                    RequiresAction: false),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification publish failed (non-fatal) for reversed payment {PaymentId}.", payment.Id);
        }

        return new NursePaymentStateResult(
            command.PeriodId, command.NurseUserId, payment.PaymentStatus.ToString(), payment.PaymentStatusReason);
    }
}

using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.Exceptions;

namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.MarkNursePaymentFailed;

public sealed class MarkNursePaymentFailedHandler
{
    private readonly INursePeriodPaymentRepository _paymentRepository;
    private readonly IAdminAuditService _auditService;

    public MarkNursePaymentFailedHandler(
        INursePeriodPaymentRepository paymentRepository,
        IAdminAuditService auditService)
    {
        _paymentRepository = paymentRepository;
        _auditService = auditService;
    }

    public async Task<NursePaymentStateResult> Handle(
        MarkNursePaymentFailedCommand command,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetAsync(command.PeriodId, command.NurseUserId, cancellationToken);
        if (payment is null)
        {
            throw new VoucherNotFoundException(command.PeriodId, command.NurseUserId);
        }

        payment.MarkPaymentFailed(command.Reason, command.AdminUserId, DateTime.UtcNow);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.AdminUserId,
                ActorRole: "Admin",
                Action: AdminAuditActions.NursePaymentFailed,
                EntityType: "NursePeriodPayment",
                EntityId: payment.Id.ToString(),
                Notes: $"Pago marcado como fallido (enfermera {command.NurseUserId}, período {command.PeriodId}). Motivo: {command.Reason}",
                MetadataJson: null),
            cancellationToken);

        return new NursePaymentStateResult(
            command.PeriodId, command.NurseUserId, payment.PaymentStatus.ToString(), payment.PaymentStatusReason);
    }
}

namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;

/// <summary>
/// Confirms a single nurse's payment for a period and delivers her comprobante (validate + email +
/// wa.me link). Extracted so the batch delivery handler can depend on the abstraction and be unit
/// tested without the full per-nurse dependency graph.
/// </summary>
public interface IConfirmNursePeriodPaymentHandler
{
    Task<ConfirmNursePeriodPaymentResult> Handle(
        ConfirmNursePeriodPaymentCommand command,
        CancellationToken cancellationToken);
}

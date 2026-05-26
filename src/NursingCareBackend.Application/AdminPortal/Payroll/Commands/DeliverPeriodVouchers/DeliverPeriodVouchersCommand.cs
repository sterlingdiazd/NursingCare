namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.DeliverPeriodVouchers;

/// <summary>
/// Admin confirms the period's bank transfer for EVERY nurse with lines and delivers each
/// nurse her comprobante de pago in one batch. A single (optional) <paramref name="BankReference"/>
/// — the bank's batch/dispersion reference — is applied to all nurses. Each nurse is processed
/// through the same per-nurse confirm+validate+deliver pipeline, so the financial-output gate and
/// idempotency hold per nurse; one nurse's failure never aborts the batch.
/// </summary>
public sealed record DeliverPeriodVouchersCommand(
    Guid PeriodId,
    Guid AdminUserId,
    string? BankReference);

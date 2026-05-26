using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;
using NursingCareBackend.Application.Exceptions;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.DeliverPeriodVouchers;

/// <summary>
/// Batch version of <see cref="ConfirmNursePeriodPaymentHandler"/>: confirms the payment and
/// delivers the comprobante for EVERY nurse with lines in a period, in one admin action. It reuses
/// the per-nurse handler (same financial-output gate, same idempotency, same demo redirect), so
/// behavior stays consistent and there is no duplicated delivery logic. A single nurse failing
/// (bad data, no email, send error) is captured as a failed item and never aborts the rest.
///
/// Each nurse is processed in its OWN DI scope (its own DbContext) so a mid-batch failure cannot
/// leave one nurse's tracked entity to be flushed into the next nurse's SaveChanges — i.e. a nurse
/// reported "failed" can never be silently persisted as Confirmed by a later iteration.
/// </summary>
public sealed class DeliverPeriodVouchersHandler
{
    private readonly IAdminPayrollRepository _payrollRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliverPeriodVouchersHandler> _logger;

    public DeliverPeriodVouchersHandler(
        IAdminPayrollRepository payrollRepository,
        IServiceScopeFactory scopeFactory,
        ILogger<DeliverPeriodVouchersHandler> logger)
    {
        _payrollRepository = payrollRepository;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<DeliverPeriodVouchersResult> Handle(
        DeliverPeriodVouchersCommand command,
        CancellationToken cancellationToken)
    {
        // The period must exist.
        var period = await _payrollRepository.GetPeriodByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            throw new VoucherNotFoundException(command.PeriodId);
        }

        // Batch delivery only on a CLOSED period (T1.2): never stamp PAGADO against a mutable net.
        // Checked up front so the whole batch fails cleanly (409) instead of every nurse erroring.
        if (!string.Equals(period.Status, nameof(PayrollPeriodStatus.Closed), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El período debe estar cerrado antes de enviar los comprobantes.");
        }

        // Every nurse with payroll lines in the period (same source the bulk-ZIP export uses).
        var allVoucherData = await _payrollRepository.GetAllVoucherDataAsync(command.PeriodId, cancellationToken);
        if (allVoucherData.Count == 0)
        {
            throw new VoucherNotFoundException(command.PeriodId);
        }

        var batchAtUtc = DateTime.UtcNow;
        var items = new List<DeliverPeriodVoucherItem>(allVoucherData.Count);

        foreach (var data in allVoucherData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Fresh scope (fresh DbContext) per nurse: isolates each confirmation so a failure
                // here can never leak tracked state into the next nurse's save.
                using var scope = _scopeFactory.CreateScope();
                var confirmHandler = scope.ServiceProvider.GetRequiredService<IConfirmNursePeriodPaymentHandler>();
                var result = await confirmHandler.Handle(
                    new ConfirmNursePeriodPaymentCommand(
                        command.PeriodId, data.NurseUserId, command.AdminUserId, command.BankReference),
                    cancellationToken);

                items.Add(new DeliverPeriodVoucherItem(
                    data.NurseUserId,
                    data.NurseDisplayName,
                    result.VoucherEmailSent,
                    result.WhatsappUrl,
                    result.RecipientLabel,
                    result.VoucherDeliveryDetail));
            }
            catch (Exception ex)
            {
                // Defensive: a single nurse erroring must not abort the batch.
                _logger.LogError(
                    ex,
                    "Batch voucher delivery failed for period {PeriodId} nurse {NurseId}",
                    command.PeriodId,
                    data.NurseUserId);
                items.Add(new DeliverPeriodVoucherItem(
                    data.NurseUserId,
                    data.NurseDisplayName,
                    VoucherEmailSent: false,
                    WhatsappUrl: string.Empty,
                    RecipientLabel: "El comprobante no se pudo enviar a la enfermera.",
                    VoucherDeliveryDetail: "No se pudo procesar el comprobante de esta enfermera."));
            }
        }

        var deliveredCount = items.Count(i => i.VoucherEmailSent);
        return new DeliverPeriodVouchersResult(
            PeriodId: command.PeriodId,
            ConfirmedAtUtc: batchAtUtc,
            TotalNurses: items.Count,
            DeliveredCount: deliveredCount,
            FailedCount: items.Count - deliveredCount,
            Items: items);
    }
}

using System.Globalization;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Application.Exceptions;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;

public sealed class ConfirmNursePeriodPaymentHandler
{
    private readonly IAdminPayrollRepository _payrollRepository;
    private readonly INursePeriodPaymentRepository _paymentRepository;
    private readonly IPayrollVoucherService _voucherService;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ConfirmNursePeriodPaymentHandler> _logger;

    public ConfirmNursePeriodPaymentHandler(
        IAdminPayrollRepository payrollRepository,
        INursePeriodPaymentRepository paymentRepository,
        IPayrollVoucherService voucherService,
        IEmailService emailService,
        IUserRepository userRepository,
        ILogger<ConfirmNursePeriodPaymentHandler> logger)
    {
        _payrollRepository = payrollRepository;
        _paymentRepository = paymentRepository;
        _voucherService = voucherService;
        _emailService = emailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ConfirmNursePeriodPaymentResult> Handle(
        ConfirmNursePeriodPaymentCommand command,
        CancellationToken cancellationToken)
    {
        // 1. The period must exist.
        var period = await _payrollRepository.GetPeriodByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            throw new VoucherNotFoundException(command.PeriodId);
        }

        // 2. The nurse must have payroll lines in this period. GetVoucherDataAsync returns
        //    null when there are no lines for the (period, nurse) pair.
        var voucherData = await _payrollRepository.GetVoucherDataAsync(
            command.PeriodId, command.NurseUserId, cancellationToken);
        if (voucherData is null)
        {
            throw new VoucherNotFoundException(command.PeriodId, command.NurseUserId);
        }

        var now = DateTime.UtcNow;
        var periodLabel = BuildPeriodLabel(voucherData.PeriodStartDate, voucherData.PeriodEndDate);

        // 3. Upsert the confirmation record (idempotent on (period, nurse)).
        var existing = await _paymentRepository.GetAsync(command.PeriodId, command.NurseUserId, cancellationToken);
        NursePeriodPayment payment;
        bool isNew;
        if (existing is null)
        {
            payment = NursePeriodPayment.Create(
                command.PeriodId,
                command.NurseUserId,
                command.AdminUserId,
                command.BankReference,
                now);
            isNew = true;
        }
        else
        {
            existing.Reconfirm(command.AdminUserId, command.BankReference, now);
            payment = existing;
            isNew = false;
        }

        // 4. Resolve the demo recipient: the logged-in admin's own email + phone.
        var admin = await _userRepository.GetByIdAsync(command.AdminUserId, cancellationToken);
        var recipientEmail = admin?.Email;
        var whatsappUrl = BuildWhatsappUrl(admin?.Phone, periodLabel);

        // 5. Generate the voucher PDF and email it to the admin (best-effort).
        var emailSent = false;
        try
        {
            var pdfBytes = await _voucherService.GenerateVoucherAsync(
                command.PeriodId, command.NurseUserId, cancellationToken);

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                payment.MarkVoucherFailed(
                    "El administrador no tiene un correo registrado para recibir el comprobante.", now);
            }
            else
            {
                var fileName = $"comprobante-{voucherData.PeriodStartDate:yyyyMMdd}-{voucherData.PeriodEndDate:yyyyMMdd}.pdf";
                await _emailService.SendWithAttachmentsAsync(
                    recipientEmail,
                    $"Comprobante de pago — período {periodLabel}",
                    BuildEmailBody(voucherData.NurseDisplayName, periodLabel),
                    new[] { new EmailAttachmentData(fileName, "application/pdf", pdfBytes) },
                    cancellationToken);

                payment.MarkVoucherDelivered(now);
                emailSent = true;
            }
        }
        catch (Exception ex)
        {
            // Delivery is best-effort for the demo: never fail the confirmation on a send error.
            _logger.LogError(
                ex,
                "Failed to deliver payroll voucher for period {PeriodId} nurse {NurseId} to admin {AdminId}",
                command.PeriodId,
                command.NurseUserId,
                command.AdminUserId);
            payment.MarkVoucherFailed(ex.Message, now);
        }

        // 6. Persist.
        if (isNew)
        {
            await _paymentRepository.AddAsync(payment, cancellationToken);
        }
        else
        {
            await _paymentRepository.SaveChangesAsync(cancellationToken);
        }

        return new ConfirmNursePeriodPaymentResult(
            PeriodId: command.PeriodId,
            NurseUserId: command.NurseUserId,
            ConfirmedAtUtc: payment.ConfirmedAtUtc,
            BankReference: payment.BankReference,
            VoucherEmailSent: emailSent,
            WhatsappUrl: whatsappUrl,
            RecipientLabel: "Modo demo: el comprobante se envió al correo y número del administrador.");
    }

    private static string BuildPeriodLabel(DateOnly start, DateOnly end) =>
        $"{start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} al {end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    private static string BuildEmailBody(string nurseDisplayName, string periodLabel)
    {
        var nurse = System.Net.WebUtility.HtmlEncode(nurseDisplayName);
        var label = System.Net.WebUtility.HtmlEncode(periodLabel);
        return $"<p>Hola,</p>" +
               $"<p>Adjunto encontrarás el comprobante de pago de <strong>{nurse}</strong> correspondiente al período {label}.</p>" +
               $"<p><em>Este es un envío en modo demostración: el comprobante se entrega al administrador.</em></p>";
    }

    /// <summary>
    /// Builds a wa.me link to the admin's own phone with a prefilled Spanish message.
    /// DR numbers are stored as 10 digits (e.g. "8099892465"); we prepend "1" for the +1
    /// country code. If the stored phone already has 11 digits starting with "1", it is
    /// used as-is. Returns "" when no usable phone is available.
    /// </summary>
    private static string BuildWhatsappUrl(string? phone, string periodLabel)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        string normalized;
        if (digits.Length == 10)
        {
            normalized = "1" + digits;
        }
        else if (digits.Length == 11 && digits[0] == '1')
        {
            normalized = digits;
        }
        else
        {
            // Unexpected length: no reliable way to build a valid +1 link for the demo.
            return string.Empty;
        }

        var message = $"Hola, te comparto tu comprobante de pago del período {periodLabel}. (Demo)";
        var encodedMessage = Uri.EscapeDataString(message);
        return $"https://wa.me/{normalized}?text={encodedMessage}";
    }
}

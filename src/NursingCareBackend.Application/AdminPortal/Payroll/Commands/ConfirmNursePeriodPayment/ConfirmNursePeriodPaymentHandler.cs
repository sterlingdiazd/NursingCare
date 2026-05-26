using System.Globalization;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.AdminPortal.Payroll.Validation;
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
    private readonly IFinancialOutputValidator _financialOutputValidator;
    private readonly ICompanyInfoProvider _companyProvider;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ConfirmNursePeriodPaymentHandler> _logger;

    public ConfirmNursePeriodPaymentHandler(
        IAdminPayrollRepository payrollRepository,
        INursePeriodPaymentRepository paymentRepository,
        IPayrollVoucherService voucherService,
        IFinancialOutputValidator financialOutputValidator,
        ICompanyInfoProvider companyProvider,
        IEmailService emailService,
        IUserRepository userRepository,
        ILogger<ConfirmNursePeriodPaymentHandler> logger)
    {
        _payrollRepository = payrollRepository;
        _paymentRepository = paymentRepository;
        _voucherService = voucherService;
        _financialOutputValidator = financialOutputValidator;
        _companyProvider = companyProvider;
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

        // 3. Upsert the confirmation record (idempotent on (period, nurse)) and persist it BEFORE
        //    generating the PDF. The voucher generator re-reads the (period, nurse) confirmation row
        //    to render the "Confirmación de pago" (PAGADO) section, so the row must already exist
        //    in the database when the PDF is built — otherwise a first-time confirmation would email
        //    a comprobante that does not yet show the payment as confirmed.
        var existing = await _paymentRepository.GetAsync(command.PeriodId, command.NurseUserId, cancellationToken);
        NursePeriodPayment payment;
        if (existing is null)
        {
            payment = NursePeriodPayment.Create(
                command.PeriodId,
                command.NurseUserId,
                command.AdminUserId,
                command.BankReference,
                now);
            await _paymentRepository.AddAsync(payment, cancellationToken);
        }
        else
        {
            existing.Reconfirm(command.AdminUserId, command.BankReference, now);
            payment = existing;
            await _paymentRepository.SaveChangesAsync(cancellationToken);
        }

        // 4. Resolve the recipient: the NURSE (the comprobante is the nurse's proof of payment).
        //    The nurse is a User, resolved the same way the handler resolves the admin user.
        var nurse = await _userRepository.GetByIdAsync(command.NurseUserId, cancellationToken);
        var nurseEmail = nurse?.Email;
        var whatsappUrl = BuildWhatsappUrl(nurse?.Phone, periodLabel);

        // 5. Generate the voucher PDF, GATE it through the financial-output validator, and only
        //    then email it to the NURSE. Delivery is best-effort for the demo, but a financial
        //    document that fails validation is BLOCKED from being sent (fail-closed) so a corrupt
        //    or unreconciled comprobante never leaves the system. The confirmation itself still
        //    succeeds; re-confirming re-runs the gate, enabling retry once the data is fixed.
        var emailSent = false;
        string? deliveryDetail = null;
        string recipientLabel = "El comprobante no se pudo enviar a la enfermera.";
        try
        {
            var pdfBytes = await _voucherService.GenerateVoucherAsync(
                command.PeriodId, command.NurseUserId, cancellationToken);

            // Build reconciliation data from the same voucher data and validate BEFORE sending.
            var company = await _companyProvider.GetAsync(cancellationToken);
            var financialData = FinancialDocumentData.ForPayrollVoucher(
                voucherData, company.Name, VoucherDocumentTitle, periodLabel);
            var validation = _financialOutputValidator.Validate(
                FinancialDocumentKind.PayrollVoucher, pdfBytes, financialData);

            if (!validation.IsValid)
            {
                // Blocked delivery: do not send (and therefore do not archive) an invalid document.
                _logger.LogWarning(
                    "Blocked payroll voucher delivery for period {PeriodId} nurse {NurseId}: {Reason}",
                    command.PeriodId,
                    command.NurseUserId,
                    validation.ReasonSummary);
                payment.MarkVoucherFailed(validation.ReasonSummary, now);
                deliveryDetail = validation.ReasonSummary;
            }
            else if (string.IsNullOrWhiteSpace(nurseEmail))
            {
                payment.MarkVoucherFailed(
                    "La enfermera no tiene un correo registrado para recibir el comprobante.", now);
                deliveryDetail = payment.DeliveryError;
            }
            else
            {
                var fileName = $"comprobante-{voucherData.PeriodStartDate:yyyyMMdd}-{voucherData.PeriodEndDate:yyyyMMdd}.pdf";
                await _emailService.SendWithAttachmentsAsync(
                    nurseEmail,
                    $"Comprobante de pago — período {periodLabel}",
                    BuildEmailBody(voucherData.NurseDisplayName, periodLabel),
                    new[] { new EmailAttachmentData(fileName, "application/pdf", pdfBytes) },
                    cancellationToken);

                payment.MarkVoucherDelivered(now);
                emailSent = true;
                deliveryDetail = "Comprobante enviado a la enfermera por correo.";
                recipientLabel = $"Comprobante enviado a la enfermera ({nurseEmail}).";
            }
        }
        catch (Exception ex)
        {
            // Delivery is best-effort for the demo: never fail the confirmation on a send error.
            _logger.LogError(
                ex,
                "Failed to deliver payroll voucher for period {PeriodId} to nurse {NurseId}",
                command.PeriodId,
                command.NurseUserId);
            payment.MarkVoucherFailed(ex.Message, now);
            deliveryDetail = payment.DeliveryError;
        }

        // 6. Persist the delivery-status outcome (the confirmation row itself was already saved in
        //    step 3 so the PDF could render the PAGADO section). The payment entity is tracked, so
        //    this flushes MarkVoucherDelivered/MarkVoucherFailed.
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        return new ConfirmNursePeriodPaymentResult(
            PeriodId: command.PeriodId,
            NurseUserId: command.NurseUserId,
            ConfirmedAtUtc: payment.ConfirmedAtUtc,
            BankReference: payment.BankReference,
            VoucherEmailSent: emailSent,
            WhatsappUrl: whatsappUrl,
            RecipientLabel: recipientLabel,
            VoucherDeliveryDetail: deliveryDetail);
    }

    /// <summary>The document title printed on the voucher PDF; the validator requires it to be present.</summary>
    private const string VoucherDocumentTitle = "COMPROBANTE DE PAGO";

    private static string BuildPeriodLabel(DateOnly start, DateOnly end) =>
        $"{start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} al {end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    private static string BuildEmailBody(string nurseDisplayName, string periodLabel)
    {
        var nurse = System.Net.WebUtility.HtmlEncode(nurseDisplayName);
        var label = System.Net.WebUtility.HtmlEncode(periodLabel);
        return $"<p>Hola {nurse},</p>" +
               $"<p>Confirmamos el pago correspondiente al período {label}. Adjunto encontrarás tu comprobante de pago.</p>" +
               $"<p>Saludos,<br/>Sol y Luna</p>";
    }

    /// <summary>
    /// Builds a wa.me link to the NURSE's phone with a prefilled Spanish message.
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

        var message = $"Hola, te enviamos tu comprobante de pago del período {periodLabel} a tu correo. (Sol y Luna)";
        var encodedMessage = Uri.EscapeDataString(message);
        return $"https://wa.me/{normalized}?text={encodedMessage}";
    }
}

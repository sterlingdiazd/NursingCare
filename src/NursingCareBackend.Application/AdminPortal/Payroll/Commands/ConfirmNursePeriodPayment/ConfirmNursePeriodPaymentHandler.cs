using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.AdminPortal.Payroll.Validation;
using NursingCareBackend.Application.Communications;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Application.Exceptions;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.Payroll;

namespace NursingCareBackend.Application.AdminPortal.Payroll.Commands.ConfirmNursePeriodPayment;

public sealed class ConfirmNursePeriodPaymentHandler : IConfirmNursePeriodPaymentHandler
{
    private readonly IAdminPayrollRepository _payrollRepository;
    private readonly INursePeriodPaymentRepository _paymentRepository;
    private readonly IPayrollVoucherService _voucherService;
    private readonly IFinancialOutputValidator _financialOutputValidator;
    private readonly ICompanyInfoProvider _companyProvider;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditService _auditService;
    private readonly IUserNotificationPublisher _userNotifications;
    private readonly IAdminNotificationPublisher _adminNotifications;
    private readonly DemoCommunicationsOptions _demoComms;
    private readonly ILogger<ConfirmNursePeriodPaymentHandler> _logger;

    public ConfirmNursePeriodPaymentHandler(
        IAdminPayrollRepository payrollRepository,
        INursePeriodPaymentRepository paymentRepository,
        IPayrollVoucherService voucherService,
        IFinancialOutputValidator financialOutputValidator,
        ICompanyInfoProvider companyProvider,
        IEmailService emailService,
        IUserRepository userRepository,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications,
        IAdminNotificationPublisher adminNotifications,
        IOptions<DemoCommunicationsOptions> demoComms,
        ILogger<ConfirmNursePeriodPaymentHandler> logger)
    {
        _payrollRepository = payrollRepository;
        _paymentRepository = paymentRepository;
        _voucherService = voucherService;
        _financialOutputValidator = financialOutputValidator;
        _companyProvider = companyProvider;
        _emailService = emailService;
        _userRepository = userRepository;
        _auditService = auditService;
        _userNotifications = userNotifications;
        _adminNotifications = adminNotifications;
        _demoComms = demoComms.Value;
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

        // 1b. Payment can only be confirmed on a CLOSED period. Confirming while Open would stamp the
        //     comprobante PAGADO against a net whose lines/deductions can still change (T1.2).
        if (!string.Equals(period.Status, nameof(PayrollPeriodStatus.Closed), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El período debe estar cerrado antes de confirmar el pago de la enfermera.");
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
        var whatsappUrl = BuildWhatsappUrl(_demoComms, nurse?.Phone, periodLabel, out var whatsappRedirectedToDemo);

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
            else if (_demoComms.Enabled && string.IsNullOrWhiteSpace(_demoComms.ContactEmail))
            {
                // Fail-closed demo with no contact email: the email layer suppresses the send, so do
                // NOT record the comprobante as delivered (mirrors the wa.me suppression guard above).
                payment.MarkVoucherFailed(
                    "Modo demo sin correo de contacto configurado; comprobante no enviado.", now);
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
                var demoSuffix = whatsappRedirectedToDemo ? " (demo)" : string.Empty;
                deliveryDetail = $"Comprobante enviado a la enfermera por correo.{demoSuffix}";
                recipientLabel = $"Comprobante enviado a la enfermera ({nurseEmail}).{demoSuffix}";
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

        // 7. Audit the money action (real money; traceable via the request CorrelationId).
        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.AdminUserId,
                ActorRole: "Admin",
                Action: AdminAuditActions.ConfirmNursePayment,
                EntityType: "NursePeriodPayment",
                EntityId: payment.Id.ToString(),
                Notes: $"Pago confirmado a enfermera {command.NurseUserId}, período {command.PeriodId}" +
                       (string.IsNullOrWhiteSpace(payment.BankReference) ? string.Empty : $", ref {payment.BankReference}") +
                       $". Comprobante: {(emailSent ? "enviado" : "no enviado")}.",
                MetadataJson: null),
            cancellationToken);

        // 8. Notify the nurse her payment was confirmed; alert admins if the comprobante could not
        //    be delivered. Best-effort: a notification failure never fails the money action.
        try
        {
            await _userNotifications.PublishToUserAsync(
                new UserNotificationPublishRequest(
                    RecipientUserId: command.NurseUserId,
                    Category: "nurse_payment_confirmed",
                    Severity: "Medium",
                    Title: "Pago confirmado",
                    Body: $"Confirmamos tu pago del período {periodLabel}. Tu comprobante está disponible.",
                    EntityType: "NursePeriodPayment",
                    EntityId: payment.Id.ToString(),
                    DeepLinkPath: "/nurse/payroll",
                    Source: "Nómina",
                    RequiresAction: false),
                cancellationToken);

            if (!emailSent)
            {
                await _adminNotifications.PublishToAdminsAsync(
                    new AdminNotificationPublishRequest(
                        Category: "voucher_delivery_failed",
                        Severity: "High",
                        Title: "Comprobante no enviado",
                        Body: $"No se pudo enviar el comprobante de una enfermera del período {periodLabel}. " +
                              $"Motivo: {deliveryDetail ?? "desconocido"}. Reintenta confirmando de nuevo.",
                        EntityType: "NursePeriodPayment",
                        EntityId: payment.Id.ToString(),
                        DeepLinkPath: $"/admin/payroll/periods/{command.PeriodId}/nurse/{command.NurseUserId}",
                        Source: "Nómina",
                        RequiresAction: true),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification publish failed (non-fatal) for nurse payment {PaymentId}.", payment.Id);
        }

        return new ConfirmNursePeriodPaymentResult(
            PeriodId: command.PeriodId,
            NurseUserId: command.NurseUserId,
            ConfirmedAtUtc: payment.ConfirmedAtUtc,
            BankReference: payment.BankReference,
            VoucherEmailSent: emailSent,
            WhatsappUrl: whatsappUrl,
            RecipientLabel: recipientLabel,
            VoucherDeliveryDetail: deliveryDetail,
            PaymentStatus: payment.PaymentStatus.ToString());
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
    /// Builds a wa.me link with a prefilled Spanish message. The target phone is normally the
    /// NURSE's phone, but while the DEMO communications redirect is enabled and a demo contact
    /// phone is configured, the link is built to that demo contact INSTEAD — so a demo never
    /// WhatsApps a real nurse. <paramref name="redirectedToDemo"/> reports whether the redirect
    /// applied. Returns "" when no usable phone is available.
    /// </summary>
    internal static string BuildWhatsappUrl(DemoCommunicationsOptions demoComms, string? nursePhone, string periodLabel, out bool redirectedToDemo)
    {
        redirectedToDemo = false;

        // Fail-closed: demo mode ON but no demo phone configured -> suppress the link rather than
        // build a wa.me to the REAL nurse. A half-configured demo must never message a real recipient.
        if (demoComms.Enabled && string.IsNullOrWhiteSpace(demoComms.ContactPhone))
        {
            return string.Empty;
        }

        var targetPhone = nursePhone;
        if (demoComms.Enabled && !string.IsNullOrWhiteSpace(demoComms.ContactPhone))
        {
            targetPhone = demoComms.ContactPhone;
            redirectedToDemo = true;
        }

        var normalized = NormalizeDominicanPhone(targetPhone);
        if (normalized is null)
        {
            redirectedToDemo = false;
            return string.Empty;
        }

        var message = $"Hola, te enviamos tu comprobante de pago del período {periodLabel} a tu correo. (Sol y Luna)";
        var encodedMessage = Uri.EscapeDataString(message);
        return $"https://wa.me/{normalized}?text={encodedMessage}";
    }

    /// <summary>
    /// Normalizes a phone for a +1 wa.me link. DR numbers are stored as 10 digits
    /// (e.g. "8099892465"); we prepend "1" for the +1 country code. An 11-digit number already
    /// starting with "1" is used as-is. Returns null when the phone is missing or an unexpected
    /// length (no reliable way to build a valid +1 link).
    /// </summary>
    private static string? NormalizeDominicanPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length == 10)
        {
            return "1" + digits;
        }

        if (digits.Length == 11 && digits[0] == '1')
        {
            return digits;
        }

        return null;
    }
}

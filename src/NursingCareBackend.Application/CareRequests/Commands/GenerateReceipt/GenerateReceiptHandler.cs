using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;

public sealed record GenerateReceiptResponse(
    Guid ReceiptId,
    string ReceiptNumber,
    string ReceiptContentBase64
);

public sealed class GenerateReceiptHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IReceiptRepository _receiptRepository;
    private readonly IPaymentValidationRepository _paymentValidationRepository;
    private readonly IReceiptPdfService _receiptPdfService;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditService _auditService;
    private readonly ICompanyInfoProvider _companyInfoProvider;

    public GenerateReceiptHandler(
        ICareRequestRepository repository,
        IReceiptRepository receiptRepository,
        IPaymentValidationRepository paymentValidationRepository,
        IReceiptPdfService receiptPdfService,
        IUserRepository userRepository,
        IAdminAuditService auditService,
        ICompanyInfoProvider companyInfoProvider)
    {
        _repository = repository;
        _receiptRepository = receiptRepository;
        _paymentValidationRepository = paymentValidationRepository;
        _receiptPdfService = receiptPdfService;
        _userRepository = userRepository;
        _auditService = auditService;
        _companyInfoProvider = companyInfoProvider;
    }

    public async Task<GenerateReceiptResponse> Handle(
        GenerateReceiptCommand command,
        CancellationToken cancellationToken)
    {
        var careRequest = await _repository.GetByIdAsync(
            command.CareRequestId,
            CareRequestAccessScope.Admin,
            cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        if (careRequest.Status != CareRequestStatus.Paid)
        {
            throw new InvalidOperationException(
                $"Receipt can only be generated for Paid care requests. Current status is {careRequest.Status}.");
        }

        // Idempotent: return existing receipt if already generated
        var existing = await _receiptRepository.GetByCareRequestIdAsync(command.CareRequestId, cancellationToken);
        if (existing is not null)
        {
            return new GenerateReceiptResponse(
                existing.Id,
                existing.ReceiptNumber,
                Convert.ToBase64String(existing.ReceiptContent));
        }

        var paymentValidation = await _paymentValidationRepository.GetByCareRequestIdAsync(
            command.CareRequestId, cancellationToken);

        var clientUser = await _userRepository.GetByIdAsync(careRequest.UserID, cancellationToken);
        var clientDisplayName = clientUser is not null
            ? (string.IsNullOrWhiteSpace(clientUser.DisplayName)
                ? (string.Join(" ", new[] { clientUser.Name, clientUser.LastName }.Where(v => !string.IsNullOrWhiteSpace(v))) is { Length: > 0 } fullName ? fullName : clientUser.Email)
                : clientUser.DisplayName)
            : string.Empty;

        var generatedAtUtc = DateTime.UtcNow;
        var date = DateOnly.FromDateTime(generatedAtUtc);
        var seq = await _receiptRepository.CountByDateAsync(date, cancellationToken) + 1;
        var receiptNumber = $"REC-{date:yyyyMMdd}-{seq:D4}";

        var companyInfo = await _companyInfoProvider.GetAsync(cancellationToken);

        var pdfData = new ReceiptPdfData(
            CareRequestId: careRequest.Id,
            ReceiptNumber: receiptNumber,
            ClientDisplayName: clientDisplayName,
            ClientIdentificationNumber: null,
            CareRequestType: careRequest.CareRequestType,
            Unit: careRequest.Unit,
            UnitType: careRequest.UnitType,
            Total: careRequest.Total,
            InvoiceNumber: careRequest.InvoiceNumber!,
            InvoicedAtUtc: careRequest.InvoicedAtUtc!.Value,
            PaidAtUtc: careRequest.PaidAtUtc!.Value,
            BankReference: paymentValidation?.BankReference ?? string.Empty,
            GeneratedAtUtc: generatedAtUtc,
            CompanyName: companyInfo.Name,
            Ncf: careRequest.Ncf,
            NcfIssuedAtUtc: careRequest.NcfIssuedAtUtc);

        var pdfBytes = _receiptPdfService.Generate(pdfData);

        var receipt = Receipt.Create(
            careRequestId: careRequest.Id,
            receiptNumber: receiptNumber,
            receiptContent: pdfBytes,
            generatedByUserId: command.ActingAdminUserId,
            generatedAtUtc: generatedAtUtc);

        await _receiptRepository.AddAsync(receipt, cancellationToken);

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.ActingAdminUserId,
                ActorRole: "Admin",
                Action: "GenerateReceipt",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: $"Recibo {receiptNumber} generado para la factura {careRequest.InvoiceNumber}.",
                MetadataJson: null),
            cancellationToken);

        return new GenerateReceiptResponse(
            receipt.Id,
            receipt.ReceiptNumber,
            Convert.ToBase64String(receipt.ReceiptContent));
    }
}

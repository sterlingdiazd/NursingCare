using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Queries.GetClientReceipt;

public sealed record ClientReceiptDownloadResult(
  Guid ReceiptId,
  string ReceiptNumber,
  byte[] Content,
  DateTime GeneratedAtUtc,
  string ContentType,
  string FileName);

public sealed class GetClientReceiptHandler
{
  private readonly ICareRequestRepository _careRequests;
  private readonly IReceiptRepository _receipts;
  private readonly IPaymentValidationRepository _paymentValidations;
  private readonly IReceiptPdfService _receiptPdfService;
  private readonly IUserRepository _users;
  private readonly ICompanyInfoProvider _companyInfoProvider;

  public GetClientReceiptHandler(
    ICareRequestRepository careRequests,
    IReceiptRepository receipts,
    IPaymentValidationRepository paymentValidations,
    IReceiptPdfService receiptPdfService,
    IUserRepository users,
    ICompanyInfoProvider companyInfoProvider)
  {
    _careRequests = careRequests;
    _receipts = receipts;
    _paymentValidations = paymentValidations;
    _receiptPdfService = receiptPdfService;
    _users = users;
    _companyInfoProvider = companyInfoProvider;
  }

  public async Task<ClientReceiptDownloadResult?> Handle(
    Guid careRequestId,
    Guid clientUserId,
    CancellationToken cancellationToken)
  {
    var careRequest = await _careRequests.GetByIdAsync(
      careRequestId,
      CareRequestAccessScope.ForClient(clientUserId),
      cancellationToken);

    if (careRequest is null)
    {
      return null;
    }

    if (careRequest.Status != CareRequestStatus.Paid)
    {
      throw new InvalidOperationException(
        $"Receipt can only be downloaded for Paid care requests. Current status is {careRequest.Status}.");
    }

    var existing = await _receipts.GetByCareRequestIdAsync(careRequestId, cancellationToken);
    if (existing is not null)
    {
      return FromReceipt(existing);
    }

    var generated = await GenerateReceiptAsync(careRequest, clientUserId, cancellationToken);
    return FromReceipt(generated);
  }

  private async Task<Receipt> GenerateReceiptAsync(
    CareRequest careRequest,
    Guid clientUserId,
    CancellationToken cancellationToken)
  {
    var paymentValidation = await _paymentValidations.GetByCareRequestIdAsync(
      careRequest.Id,
      cancellationToken);

    var clientUser = await _users.GetByIdAsync(careRequest.UserID, cancellationToken);
    var clientDisplayName = ResolveClientDisplayName(clientUser);

    var generatedAtUtc = DateTime.UtcNow;
    var date = DateOnly.FromDateTime(generatedAtUtc);
    var sequence = await _receipts.CountByDateAsync(date, cancellationToken) + 1;
    var receiptNumber = $"REC-{date:yyyyMMdd}-{sequence:D4}";

    var companyInfo = await _companyInfoProvider.GetAsync(cancellationToken);

    var pdfData = new ReceiptPdfData(
      CareRequestId: careRequest.Id,
      ReceiptNumber: receiptNumber,
      ClientDisplayName: clientDisplayName,
      ClientIdentificationNumber: clientUser?.IdentificationNumber,
      CareRequestType: careRequest.CareRequestType,
      Unit: careRequest.Unit,
      UnitType: careRequest.UnitType,
      Total: careRequest.Total,
      InvoiceNumber: careRequest.InvoiceNumber!,
      InvoicedAtUtc: careRequest.InvoicedAtUtc!.Value,
      PaidAtUtc: careRequest.PaidAtUtc!.Value,
      BankReference: paymentValidation?.BankReference ?? string.Empty,
      GeneratedAtUtc: generatedAtUtc,
      CompanyName: companyInfo.Name);

    var receipt = Receipt.Create(
      careRequestId: careRequest.Id,
      receiptNumber: receiptNumber,
      receiptContent: _receiptPdfService.Generate(pdfData),
      generatedByUserId: clientUserId,
      generatedAtUtc: generatedAtUtc);

    await _receipts.AddAsync(receipt, cancellationToken);
    return receipt;
  }

  private static string ResolveClientDisplayName(NursingCareBackend.Domain.Identity.User? user)
  {
    if (user is null)
    {
      return string.Empty;
    }

    if (!string.IsNullOrWhiteSpace(user.DisplayName))
    {
      return user.DisplayName;
    }

    var fullName = string.Join(
      " ",
      new[] { user.Name, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));

    return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
  }

  private static ClientReceiptDownloadResult FromReceipt(Receipt receipt)
    => new(
      receipt.Id,
      receipt.ReceiptNumber,
      receipt.ReceiptContent,
      receipt.GeneratedAtUtc,
      "application/pdf",
      $"recibo-{receipt.ReceiptNumber}.pdf");
}

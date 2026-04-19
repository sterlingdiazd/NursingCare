using NursingCareBackend.Application.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Queries.GetReceipt;

public sealed record GetReceiptResponse(
    Guid ReceiptId,
    string ReceiptNumber,
    string ReceiptContentBase64,
    DateTime GeneratedAtUtc
);

public sealed class GetReceiptHandler
{
    private readonly IReceiptRepository _receiptRepository;

    public GetReceiptHandler(IReceiptRepository receiptRepository)
    {
        _receiptRepository = receiptRepository;
    }

    public async Task<GetReceiptResponse?> Handle(
        Guid careRequestId,
        CancellationToken cancellationToken)
    {
        var receipt = await _receiptRepository.GetByCareRequestIdAsync(careRequestId, cancellationToken);

        if (receipt is null)
        {
            return null;
        }

        return new GetReceiptResponse(
            receipt.Id,
            receipt.ReceiptNumber,
            Convert.ToBase64String(receipt.ReceiptContent),
            receipt.GeneratedAtUtc);
    }
}

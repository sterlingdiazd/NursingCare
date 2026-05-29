using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.PaymentOcr;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.AssessPaymentProofOcr;

public sealed class AssessPaymentProofOcrHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly IPaymentProofOcrService _ocrService;

    public AssessPaymentProofOcrHandler(
        ICareRequestRepository repository,
        IPaymentProofOcrService ocrService)
    {
        _repository = repository;
        _ocrService = ocrService;
    }

    public async Task<PaymentOcrAssessment> Handle(
        AssessPaymentProofOcrCommand command,
        CancellationToken cancellationToken)
    {
        var careRequest = await _repository.GetByIdAsync(
            command.CareRequestId,
            CareRequestAccessScope.ForClient(command.ActingUserId),
            cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        if (careRequest.Status != CareRequestStatus.Invoiced)
        {
            throw new InvalidOperationException(
                $"Solo se puede leer un comprobante de una solicitud facturada. Estado actual: {careRequest.Status}.");
        }

        return await _ocrService.AssessAsync(
            new PaymentProofOcrInput(
                careRequest.Id,
                command.ImageContent,
                command.ContentType,
                careRequest.Total),
            cancellationToken);
    }
}

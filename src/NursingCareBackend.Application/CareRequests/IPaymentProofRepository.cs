using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests;

public interface IPaymentProofRepository
{
    Task AddAsync(PaymentProof proof, CancellationToken cancellationToken);
    Task<PaymentProof?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PaymentProof?> GetLatestByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken);
}

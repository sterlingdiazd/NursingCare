using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests;

public interface IPaymentValidationRepository
{
    Task AddAsync(PaymentValidation paymentValidation, CancellationToken cancellationToken);
    Task<PaymentValidation?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken);
}

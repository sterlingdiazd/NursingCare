using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests;

public interface IPaymentValidationRepository
{
    Task AddAsync(PaymentValidation paymentValidation, CancellationToken cancellationToken);
    Task<PaymentValidation?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Anti-fraud: true if this bank reference already produced a CONFIRMED payment on a DIFFERENT
    /// care request — i.e. the same real bank transfer would be counted twice. Compared trimmed and
    /// case-insensitively (independent of DB collation); blank reference returns false.
    /// </summary>
    Task<bool> IsBankReferenceUsedAsync(
        string bankReference, Guid excludeCareRequestId, CancellationToken cancellationToken);
}

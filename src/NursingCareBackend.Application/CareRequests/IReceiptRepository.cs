using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests;

public interface IReceiptRepository
{
    Task<Receipt?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken);
    Task AddAsync(Receipt receipt, CancellationToken cancellationToken);
    Task<int> CountByDateAsync(DateOnly date, CancellationToken cancellationToken);
}

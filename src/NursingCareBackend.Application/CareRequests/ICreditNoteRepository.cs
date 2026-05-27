using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests;

public interface ICreditNoteRepository
{
    /// <summary>Sum of all credit-note amounts already issued for a request (0 when none).</summary>
    Task<decimal> GetTotalCreditedAsync(Guid careRequestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CreditNote>> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken);

    Task AddAsync(CreditNote creditNote, CancellationToken cancellationToken);
}

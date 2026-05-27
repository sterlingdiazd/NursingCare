using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.CareRequests;

public sealed class CreditNoteRepository : ICreditNoteRepository
{
    private readonly NursingCareDbContext _dbContext;

    public CreditNoteRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<decimal> GetTotalCreditedAsync(Guid careRequestId, CancellationToken cancellationToken)
    {
        // Nullable Sum so an empty set returns null (→ 0) instead of throwing across providers.
        return await _dbContext.CreditNotes
            .AsNoTracking()
            .Where(c => c.CareRequestId == careRequestId)
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyList<CreditNote>> GetByCareRequestIdAsync(
        Guid careRequestId, CancellationToken cancellationToken)
    {
        return await _dbContext.CreditNotes
            .AsNoTracking()
            .Where(c => c.CareRequestId == careRequestId)
            .OrderBy(c => c.IssuedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CreditNote creditNote, CancellationToken cancellationToken)
    {
        await _dbContext.CreditNotes.AddAsync(creditNote, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

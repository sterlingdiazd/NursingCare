using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.CareRequests;

public sealed class ReceiptRepository : IReceiptRepository
{
    private readonly NursingCareDbContext _dbContext;

    public ReceiptRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Receipt?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken)
    {
        return _dbContext.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CareRequestId == careRequestId, cancellationToken);
    }

    public async Task AddAsync(Receipt receipt, CancellationToken cancellationToken)
    {
        await _dbContext.Receipts.AddAsync(receipt, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        // Count receipts generated on a given date to generate sequential receipt numbers
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = startOfDay.AddDays(1);

        return _dbContext.Receipts
            .AsNoTracking()
            .CountAsync(r => r.GeneratedAtUtc >= startOfDay && r.GeneratedAtUtc < endOfDay, cancellationToken);
    }
}

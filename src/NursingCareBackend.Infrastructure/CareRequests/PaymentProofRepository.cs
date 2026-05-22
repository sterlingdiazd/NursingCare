using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.CareRequests;

public sealed class PaymentProofRepository : IPaymentProofRepository
{
    private readonly NursingCareDbContext _dbContext;

    public PaymentProofRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(PaymentProof proof, CancellationToken cancellationToken)
    {
        _dbContext.PaymentProofs.Add(proof);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PaymentProof?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.PaymentProofs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PaymentProof?> GetLatestByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken)
        => _dbContext.PaymentProofs.AsNoTracking()
            .Where(p => p.CareRequestId == careRequestId)
            .OrderByDescending(p => p.UploadedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}

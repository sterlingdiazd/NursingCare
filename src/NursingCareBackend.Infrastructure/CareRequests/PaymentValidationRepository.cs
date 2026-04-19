using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.CareRequests;

public sealed class PaymentValidationRepository : IPaymentValidationRepository
{
    private readonly NursingCareDbContext _dbContext;

    public PaymentValidationRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(PaymentValidation paymentValidation, CancellationToken cancellationToken)
    {
        await _dbContext.PaymentValidations.AddAsync(paymentValidation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PaymentValidation?> GetByCareRequestIdAsync(Guid careRequestId, CancellationToken cancellationToken)
    {
        return _dbContext.PaymentValidations
            .AsNoTracking()
            .FirstOrDefaultAsync(pv => pv.CareRequestId == careRequestId, cancellationToken);
    }
}

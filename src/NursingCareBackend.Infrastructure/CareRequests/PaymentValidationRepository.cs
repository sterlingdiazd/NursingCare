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

    public Task<bool> IsBankReferenceUsedAsync(
        string bankReference, Guid excludeCareRequestId, CancellationToken cancellationToken)
    {
        var normalized = bankReference?.Trim().ToUpper();
        if (string.IsNullOrEmpty(normalized))
        {
            return Task.FromResult(false);
        }

        // Compare case-insensitively WITHOUT relying on the DB collation: UPPER() both sides. The
        // table is tiny (one admin, low volume) so the non-sargable comparison is irrelevant.
        return _dbContext.PaymentValidations
            .AsNoTracking()
            .AnyAsync(
                pv => pv.CareRequestId != excludeCareRequestId && pv.BankReference.ToUpper() == normalized,
                cancellationToken);
    }
}

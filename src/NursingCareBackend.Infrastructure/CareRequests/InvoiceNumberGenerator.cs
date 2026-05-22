using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Infrastructure.Fiscal;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.CareRequests;

/// <summary>
/// Monthly-sequential invoice numbers. Informal: {Prefix}-{yyyyMM}-{####} (e.g. SOL-202605-0001).
/// e-CF (when enabled): {NcfType}{##########} (e.g. E320000000001).
/// </summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly NursingCareDbContext _dbContext;
    private readonly IOptions<FiscalOptions> _fiscal;

    public InvoiceNumberGenerator(NursingCareDbContext dbContext, IOptions<FiscalOptions> fiscal)
    {
        _dbContext = dbContext;
        _fiscal = fiscal;
    }

    public async Task<string> NextAsync(DateTime invoiceDateUtc, CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(invoiceDateUtc.Year, invoiceDateUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var seq = await _dbContext.CareRequests
            .AsNoTracking()
            .CountAsync(c => c.InvoicedAtUtc >= monthStart && c.InvoicedAtUtc < monthEnd, cancellationToken) + 1;

        var f = _fiscal.Value;
        return f.NcfEnabled
            ? $"{f.NcfType}{seq:D10}"
            : $"{f.InvoiceNumberPrefix}-{invoiceDateUtc:yyyyMM}-{seq:D4}";
    }
}

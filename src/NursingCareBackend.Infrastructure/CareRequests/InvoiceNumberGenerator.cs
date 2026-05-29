using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.CareRequests;

/// <summary>
/// Generates the two independent document-number sequences for a care request:
/// <list type="bullet">
///   <item>Proforma / cuenta de cobro (non-fiscal): {Prefix}-{yyyyMM}-{####} (e.g. SOL-202605-0001),
///   monthly-sequential by <see cref="Domain.CareRequests.CareRequest.InvoicedAtUtc"/>.</item>
///   <item>e-NCF (DGII fiscal comprobante, only when payment is confirmed in fiscal mode):
///   {NcfType}{##########} (e.g. E320000000001), counted over the independent
///   <see cref="Domain.CareRequests.CareRequest.NcfIssuedAtUtc"/> sequence.</item>
/// </list>
/// Keeping the two counters separate is what protects the DGII sequence: completing or voiding a
/// request never advances the e-NCF number, so there are no gaps and no spurious Notas de Crédito.
/// </summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly NursingCareDbContext _dbContext;
    private readonly IFiscalSettingsProvider _fiscal;

    public InvoiceNumberGenerator(NursingCareDbContext dbContext, IFiscalSettingsProvider fiscal)
    {
        _dbContext = dbContext;
        _fiscal = fiscal;
    }

    public async Task<bool> IsFiscalModeEnabledAsync(CancellationToken cancellationToken = default)
        => (await _fiscal.GetAsync(cancellationToken)).NcfEnabled;

    public async Task<string> NextProformaAsync(DateTime invoiceDateUtc, CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(invoiceDateUtc.Year, invoiceDateUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var seq = await _dbContext.CareRequests
            .AsNoTracking()
            .CountAsync(c => c.InvoicedAtUtc >= monthStart && c.InvoicedAtUtc < monthEnd, cancellationToken) + 1;

        var f = await _fiscal.GetAsync(cancellationToken);
        return $"{f.InvoiceNumberPrefix}-{invoiceDateUtc:yyyyMM}-{seq:D4}";
    }

    public async Task<string> NextFiscalNcfAsync(DateTime issuedAtUtc, CancellationToken cancellationToken)
    {
        // Single ever-growing fiscal sequence (DGII e-NCFs are NOT reset monthly). Count every
        // request that already carries an issued NCF; the next one is that count + 1.
        var seq = await _dbContext.CareRequests
            .AsNoTracking()
            .CountAsync(c => c.NcfIssuedAtUtc != null, cancellationToken) + 1;

        var f = await _fiscal.GetAsync(cancellationToken);
        return $"{f.NcfType}{seq:D10}";
    }
}

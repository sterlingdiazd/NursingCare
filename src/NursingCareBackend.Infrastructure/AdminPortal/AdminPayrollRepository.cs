using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminPayrollRepository : IAdminPayrollRepository
{
    private readonly NursingCareDbContext _dbContext;

    public AdminPayrollRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminPayrollPeriodListResult> GetPeriodsAsync(
        AdminPayrollPeriodListFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PayrollPeriods.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<PayrollPeriodStatus>(filter.Status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(p => p.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var periods = await query
            .OrderByDescending(p => p.StartDate)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var periodIds = periods.Select(p => p.Id).ToList();
        var lineCounts = periodIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _dbContext.PayrollLines
                .AsNoTracking()
                .Where(l => periodIds.Contains(l.PayrollPeriodId))
                .GroupBy(l => l.PayrollPeriodId)
                .Select(g => new { PeriodId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PeriodId, x => x.Count, cancellationToken);

        var items = periods
            .Select(p => new AdminPayrollPeriodListItem(
                p.Id,
                p.StartDate,
                p.EndDate,
                p.CutoffDate,
                p.PaymentDate,
                p.Status.ToString(),
                p.CreatedAtUtc,
                p.ClosedAtUtc,
                lineCounts.GetValueOrDefault(p.Id, 0)))
            .ToList()
            .AsReadOnly();

        return new AdminPayrollPeriodListResult(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<AdminPayrollPeriodDetail?> GetPeriodByIdAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return null;

        var lines = await _dbContext.PayrollLines
            .AsNoTracking()
            .Where(l => l.PayrollPeriodId == periodId)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var nurseIds = lines.Select(l => l.NurseUserId).Distinct().ToList();
        var nurseLookup = await BuildNurseLookupAsync(nurseIds, cancellationToken);

        var lineItems = lines
            .Select(l => new AdminPayrollLineItem(
                l.Id,
                l.NurseUserId,
                nurseLookup.GetValueOrDefault(l.NurseUserId, l.NurseUserId.ToString()),
                l.ServiceExecutionId,
                l.Description,
                l.BaseCompensation,
                l.TransportIncentive,
                l.ComplexityBonus,
                l.MedicalSuppliesCompensation,
                l.AdjustmentsTotal,
                l.DeductionsTotal,
                l.NetCompensation,
                l.CreatedAtUtc))
            .ToList()
            .AsReadOnly();

        var staffSummary = lines
            .GroupBy(l => l.NurseUserId)
            .Select(g => new AdminPayrollStaffSummary(
                g.Key,
                nurseLookup.GetValueOrDefault(g.Key, g.Key.ToString()),
                g.Count(),
                g.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal),
                g.Sum(l => l.TransportIncentive),
                g.Sum(l => l.AdjustmentsTotal),
                g.Sum(l => l.DeductionsTotal),
                g.Sum(l => l.NetCompensation)))
            .OrderByDescending(s => s.NetCompensation)
            .ToList()
            .AsReadOnly();

        return new AdminPayrollPeriodDetail(
            period.Id,
            period.StartDate,
            period.EndDate,
            period.CutoffDate,
            period.PaymentDate,
            period.Status.ToString(),
            period.CreatedAtUtc,
            period.ClosedAtUtc,
            lineItems,
            staffSummary);
    }

    public async Task<Guid> CreatePeriodAsync(
        DateOnly startDate,
        DateOnly endDate,
        DateOnly cutoffDate,
        DateOnly paymentDate,
        CancellationToken cancellationToken)
    {
        var period = PayrollPeriod.Create(startDate, endDate, cutoffDate, paymentDate, DateTime.UtcNow);
        _dbContext.PayrollPeriods.Add(period);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return period.Id;
    }

    public async Task<bool> ClosePeriodAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return false;

        period.Close(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AdminPayrollLineItem>> GetPeriodLinesAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var lines = await _dbContext.PayrollLines
            .AsNoTracking()
            .Where(l => l.PayrollPeriodId == periodId)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var nurseIds = lines.Select(l => l.NurseUserId).Distinct().ToList();
        var nurseLookup = await BuildNurseLookupAsync(nurseIds, cancellationToken);

        return lines
            .Select(l => new AdminPayrollLineItem(
                l.Id,
                l.NurseUserId,
                nurseLookup.GetValueOrDefault(l.NurseUserId, l.NurseUserId.ToString()),
                l.ServiceExecutionId,
                l.Description,
                l.BaseCompensation,
                l.TransportIncentive,
                l.ComplexityBonus,
                l.MedicalSuppliesCompensation,
                l.AdjustmentsTotal,
                l.DeductionsTotal,
                l.NetCompensation,
                l.CreatedAtUtc))
            .ToList()
            .AsReadOnly();
    }

    private async Task<Dictionary<Guid, string>> BuildNurseLookupAsync(
        IReadOnlyCollection<Guid> nurseIds,
        CancellationToken cancellationToken)
    {
        if (nurseIds.Count == 0) return new Dictionary<Guid, string>();

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => nurseIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.LastName, u.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u =>
            {
                var fullName = string.Join(" ", new[] { u.Name, u.LastName }
                    .Where(v => !string.IsNullOrWhiteSpace(v)));
                return string.IsNullOrWhiteSpace(fullName) ? u.Email : fullName;
            });
    }
}

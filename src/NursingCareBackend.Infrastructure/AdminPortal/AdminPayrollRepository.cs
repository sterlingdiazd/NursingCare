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

    public async Task<AdminDeductionListResult> GetDeductionsAsync(Guid? nurseId, Guid? periodId, CancellationToken cancellationToken)
    {
        var query = _dbContext.DeductionRecords.AsNoTracking();

        if (nurseId.HasValue)
            query = query.Where(d => d.NurseUserId == nurseId.Value);
        if (periodId.HasValue)
            query = query.Where(d => d.PayrollPeriodId == periodId.Value);

        var deductions = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var nurseIds = deductions.Select(d => d.NurseUserId).Distinct().ToList();
        var nurseLookup = await BuildNurseLookupAsync(nurseIds, cancellationToken);

        var items = deductions
            .Select(d => new AdminDeductionListItem(
                d.Id,
                d.NurseUserId,
                nurseLookup.GetValueOrDefault(d.NurseUserId, d.NurseUserId.ToString()),
                d.PayrollPeriodId,
                d.Label,
                d.Amount,
                d.DeductionType.ToString(),
                d.CreatedAtUtc))
            .ToList()
            .AsReadOnly();

        return new AdminDeductionListResult(items, items.Count);
    }

    public async Task<Guid> CreateDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DeductionType>(request.DeductionType, ignoreCase: true, out var deductionType))
            throw new ArgumentException($"Tipo de deducción invalido: {request.DeductionType}");

        var deduction = DeductionRecord.Create(
            request.NurseUserId,
            request.PayrollPeriodId,
            deductionType,
            request.Label,
            request.Amount,
            null, // notes
            DateTime.UtcNow,
            DateTime.UtcNow);

        _dbContext.DeductionRecords.Add(deduction);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return deduction.Id;
    }

    public async Task<bool> DeleteDeductionAsync(Guid deductionId, CancellationToken cancellationToken)
    {
        var deduction = await _dbContext.DeductionRecords
            .FirstOrDefaultAsync(d => d.Id == deductionId, cancellationToken);

        if (deduction is null) return false;

        _dbContext.DeductionRecords.Remove(deduction);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminCompensationAdjustmentListResult> GetAdjustmentsAsync(Guid? executionId, CancellationToken cancellationToken)
    {
        var query = _dbContext.CompensationAdjustments.AsNoTracking();

        if (executionId.HasValue)
            query = query.Where(a => a.ServiceExecutionId == executionId.Value);

        var adjustments = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var executionIds = adjustments.Select(a => a.ServiceExecutionId).Distinct().ToList();
        var executionLookup = await BuildExecutionLookupAsync(executionIds, cancellationToken);

        var items = adjustments
            .Select(a => new AdminCompensationAdjustmentListItem(
                a.Id,
                a.ServiceExecutionId,
                executionLookup.GetValueOrDefault(a.ServiceExecutionId, a.ServiceExecutionId.ToString()),
                a.Label,
                a.Amount,
                a.CreatedAtUtc))
            .ToList()
            .AsReadOnly();

        return new AdminCompensationAdjustmentListResult(items, items.Count);
    }

    public async Task<Guid> CreateAdjustmentAsync(CreateCompensationAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = CompensationAdjustment.Create(
            request.ServiceExecutionId,
            request.Label,
            request.Amount,
            null, // notes
            DateTime.UtcNow);

        _dbContext.CompensationAdjustments.Add(adjustment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return adjustment.Id;
    }

    public async Task<bool> DeleteAdjustmentAsync(Guid adjustmentId, CancellationToken cancellationToken)
    {
        var adjustment = await _dbContext.CompensationAdjustments
            .FirstOrDefaultAsync(a => a.Id == adjustmentId, cancellationToken);

        if (adjustment is null) return false;

        _dbContext.CompensationAdjustments.Remove(adjustment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Dictionary<Guid, string>> BuildExecutionLookupAsync(IReadOnlyCollection<Guid> executionIds, CancellationToken cancellationToken)
    {
        if (executionIds.Count == 0) return new Dictionary<Guid, string>();

        var executions = await _dbContext.ServiceExecutions
            .AsNoTracking()
            .Where(se => executionIds.Contains(se.Id))
            .Select(se => new { se.Id, se.NurseUserId })
            .ToListAsync(cancellationToken);

        var nurseIds = executions.Select(se => se.NurseUserId).Distinct().ToList();
        var nurseLookup = await BuildNurseLookupAsync(nurseIds, cancellationToken);

        return executions.ToDictionary(
            se => se.Id,
            se => nurseLookup.GetValueOrDefault(se.NurseUserId, se.NurseUserId.ToString()));
    }
}

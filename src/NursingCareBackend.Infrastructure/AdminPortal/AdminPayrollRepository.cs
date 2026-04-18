using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

file static class StringExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}


public sealed class AdminPayrollRepository : IAdminPayrollRepository, INursePayrollRepository
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

    public async Task<IReadOnlyList<NursePeriodHistoryItem>> GetNursePeriodHistoryAsync(
        Guid nurseId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Batch query: load lines for nurse, grouped by period
        var linesByPeriod = await _dbContext.PayrollLines
            .AsNoTracking()
            .Where(l => l.NurseUserId == nurseId)
            .GroupBy(l => l.PayrollPeriodId)
            .Select(g => new
            {
                PeriodId = g.Key,
                ServiceCount = g.Count(),
                TotalCompensation = g.Sum(l => l.NetCompensation)
            })
            .ToListAsync(cancellationToken);

        var periodIds = linesByPeriod.Select(x => x.PeriodId).ToList();

        var periods = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(p => periodIds.Contains(p.Id))
            .OrderByDescending(p => p.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var linesByPeriodDict = linesByPeriod.ToDictionary(x => x.PeriodId);

        return periods
            .Select(p =>
            {
                var summary = linesByPeriodDict.GetValueOrDefault(p.Id);
                return new NursePeriodHistoryItem(
                    p.Id,
                    p.StartDate,
                    p.EndDate,
                    p.Status.ToString(),
                    summary?.ServiceCount ?? 0,
                    summary?.TotalCompensation ?? 0m);
            })
            .ToList()
            .AsReadOnly();
    }

    public async Task<int> CountNurseLinesInOpenPeriodsAsync(Guid nurseId, CancellationToken cancellationToken)
    {
        var openPeriodIds = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(p => p.Status == PayrollPeriodStatus.Open)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return await _dbContext.PayrollLines
            .AsNoTracking()
            .CountAsync(l => l.NurseUserId == nurseId && openPeriodIds.Contains(l.PayrollPeriodId), cancellationToken);
    }

    public async Task<int> CountNurseLinesInClosedPeriodsAsync(Guid nurseId, CancellationToken cancellationToken)
    {
        var closedPeriodIds = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(p => p.Status == PayrollPeriodStatus.Closed)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return await _dbContext.PayrollLines
            .AsNoTracking()
            .CountAsync(l => l.NurseUserId == nurseId && closedPeriodIds.Contains(l.PayrollPeriodId), cancellationToken);
    }

    public async Task<NursePeriodDetail?> GetNursePeriodDetailAsync(Guid periodId, Guid nurseId, CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return null;

        var lines = await _dbContext.PayrollLines
            .AsNoTracking()
            .Where(l => l.PayrollPeriodId == periodId && l.NurseUserId == nurseId)
            .ToListAsync(cancellationToken);

        var executionIds = lines
            .Where(l => l.ServiceExecutionId.HasValue)
            .Select(l => l.ServiceExecutionId!.Value)
            .ToList();

        var executions = executionIds.Count == 0
            ? new Dictionary<Guid, (Guid CareRequestId, DateOnly ServiceDate)>()
            : await _dbContext.ServiceExecutions
                .AsNoTracking()
                .Where(e => executionIds.Contains(e.Id))
                .Select(e => new { e.Id, e.CareRequestId, e.ServiceDate })
                .ToDictionaryAsync(e => e.Id, e => (e.CareRequestId, e.ServiceDate), cancellationToken);

        var serviceRows = lines
            .Select(l =>
            {
                var execData = l.ServiceExecutionId.HasValue && executions.TryGetValue(l.ServiceExecutionId.Value, out var ed)
                    ? ed
                    : (CareRequestId: Guid.Empty, ServiceDate: DateOnly.MinValue);

                return new NurseServiceRow(
                    l.ServiceExecutionId ?? Guid.Empty,
                    execData.CareRequestId,
                    execData.ServiceDate,
                    l.BaseCompensation,
                    l.TransportIncentive,
                    l.ComplexityBonus,
                    l.MedicalSuppliesCompensation,
                    l.AdjustmentsTotal,
                    l.DeductionsTotal,
                    l.NetCompensation);
            })
            .ToList()
            .AsReadOnly();

        return new NursePeriodDetail(
            period.Id,
            period.StartDate,
            period.EndDate,
            period.Status.ToString(),
            period.CutoffDate,
            period.PaymentDate,
            lines.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation),
            lines.Sum(l => l.DeductionsTotal),
            lines.Sum(l => l.AdjustmentsTotal),
            lines.Sum(l => l.NetCompensation),
            serviceRows);
    }

    public async Task<PayrollVoucherData?> GetVoucherDataAsync(
        Guid periodId,
        Guid nurseId,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return null;

        var lines = await _dbContext.PayrollLines
            .AsNoTracking()
            .Where(l => l.PayrollPeriodId == periodId && l.NurseUserId == nurseId)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0) return null;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == nurseId)
            .Select(u => new { u.Id, u.Name, u.LastName, u.Email, u.IdentificationNumber })
            .FirstOrDefaultAsync(cancellationToken);

        var deductions = await _dbContext.DeductionRecords
            .AsNoTracking()
            .Where(d => d.NurseUserId == nurseId && d.PayrollPeriodId == periodId)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var displayName = user is null
            ? nurseId.ToString()
            : string.Join(" ", new[] { user.Name, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)))
                .NullIfEmpty() ?? user.Email;

        var lineItems = lines
            .Select(l => new VoucherLineItem
            {
                Description = l.Description,
                BaseCompensation = l.BaseCompensation,
                TransportIncentive = l.TransportIncentive,
                ComplexityBonus = l.ComplexityBonus,
                MedicalSuppliesCompensation = l.MedicalSuppliesCompensation,
                AdjustmentsTotal = l.AdjustmentsTotal,
                DeductionsTotal = l.DeductionsTotal,
                NetCompensation = l.NetCompensation,
            })
            .ToList()
            .AsReadOnly();

        var deductionItems = deductions
            .Select(d => new VoucherDeductionItem
            {
                Label = d.Label,
                DeductionTypeLabel = d.DeductionType switch
                {
                    DeductionType.Loan => "Prestamo",
                    DeductionType.Advance => "Avance",
                    _ => "Otro",
                },
                Amount = d.Amount,
            })
            .ToList()
            .AsReadOnly();

        return new PayrollVoucherData
        {
            PeriodId = period.Id,
            PeriodStartDate = period.StartDate,
            PeriodEndDate = period.EndDate,
            PaymentDate = period.PaymentDate,
            PeriodStatus = period.Status.ToString(),
            NurseUserId = nurseId,
            NurseDisplayName = displayName,
            NurseCedula = user?.IdentificationNumber,
            Lines = lineItems,
            Deductions = deductionItems,
            TotalGross = lines.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation),
            TotalTransport = lines.Sum(l => l.TransportIncentive),
            TotalComplexity = lines.Sum(l => l.ComplexityBonus),
            TotalSupplies = lines.Sum(l => l.MedicalSuppliesCompensation),
            TotalAdjustments = lines.Sum(l => l.AdjustmentsTotal),
            TotalDeductions = lines.Sum(l => l.DeductionsTotal),
            NetCompensation = lines.Sum(l => l.NetCompensation),
        };
    }

    public async Task<IReadOnlyList<PayrollVoucherData>> GetAllVoucherDataAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return [];

        var allLines = await _dbContext.PayrollLines
            .AsNoTracking()
            .Where(l => l.PayrollPeriodId == periodId)
            .OrderBy(l => l.NurseUserId)
            .ThenBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (allLines.Count == 0) return [];

        var nurseIds = allLines.Select(l => l.NurseUserId).Distinct().ToList();

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => nurseIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.LastName, u.Email, u.IdentificationNumber })
            .ToListAsync(cancellationToken);

        var userLookup = users.ToDictionary(u => u.Id);

        var allDeductions = await _dbContext.DeductionRecords
            .AsNoTracking()
            .Where(d => nurseIds.Contains(d.NurseUserId) && d.PayrollPeriodId == periodId)
            .OrderBy(d => d.NurseUserId)
            .ThenBy(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var deductionsByNurse = allDeductions
            .GroupBy(d => d.NurseUserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<PayrollVoucherData>();

        foreach (var nurseGroup in allLines.GroupBy(l => l.NurseUserId))
        {
            var nurseId = nurseGroup.Key;
            var lines = nurseGroup.ToList();
            var user = userLookup.GetValueOrDefault(nurseId);

            var displayName = user is null
                ? nurseId.ToString()
                : string.Join(" ", new[] { user.Name, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)))
                    .NullIfEmpty() ?? user.Email;

            var lineItems = lines
                .Select(l => new VoucherLineItem
                {
                    Description = l.Description,
                    BaseCompensation = l.BaseCompensation,
                    TransportIncentive = l.TransportIncentive,
                    ComplexityBonus = l.ComplexityBonus,
                    MedicalSuppliesCompensation = l.MedicalSuppliesCompensation,
                    AdjustmentsTotal = l.AdjustmentsTotal,
                    DeductionsTotal = l.DeductionsTotal,
                    NetCompensation = l.NetCompensation,
                })
                .ToList()
                .AsReadOnly();

            var nurseDeductions = deductionsByNurse.GetValueOrDefault(nurseId, []);
            var deductionItems = nurseDeductions
                .Select(d => new VoucherDeductionItem
                {
                    Label = d.Label,
                    DeductionTypeLabel = d.DeductionType switch
                    {
                        DeductionType.Loan => "Prestamo",
                        DeductionType.Advance => "Avance",
                        _ => "Otro",
                    },
                    Amount = d.Amount,
                })
                .ToList()
                .AsReadOnly();

            result.Add(new PayrollVoucherData
            {
                PeriodId = period.Id,
                PeriodStartDate = period.StartDate,
                PeriodEndDate = period.EndDate,
                PaymentDate = period.PaymentDate,
                PeriodStatus = period.Status.ToString(),
                NurseUserId = nurseId,
                NurseDisplayName = displayName,
                NurseCedula = user?.IdentificationNumber,
                Lines = lineItems,
                Deductions = deductionItems,
                TotalGross = lines.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation),
                TotalTransport = lines.Sum(l => l.TransportIncentive),
                TotalComplexity = lines.Sum(l => l.ComplexityBonus),
                TotalSupplies = lines.Sum(l => l.MedicalSuppliesCompensation),
                TotalAdjustments = lines.Sum(l => l.AdjustmentsTotal),
                TotalDeductions = lines.Sum(l => l.DeductionsTotal),
                NetCompensation = lines.Sum(l => l.NetCompensation),
            });
        }

        return result.AsReadOnly();
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

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Exceptions;
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

        // Per-period line count and gross payout (sum of line components incl. adjustments).
        var lineAggregates = periodIds.Count == 0
            ? new Dictionary<Guid, (int Count, decimal Gross)>()
            : (await _dbContext.PayrollLines
                .AsNoTracking()
                .Where(l => periodIds.Contains(l.PayrollPeriodId))
                .GroupBy(l => l.PayrollPeriodId)
                .Select(g => new
                {
                    PeriodId = g.Key,
                    Count = g.Count(),
                    Gross = g.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal),
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.PeriodId, x => (x.Count, x.Gross));

        // Period-level deductions (per nurse, subtracted once) — summed per period for the net.
        var deductionsByPeriod = periodIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _dbContext.DeductionRecords
                .AsNoTracking()
                .Where(d => d.PayrollPeriodId != null && periodIds.Contains(d.PayrollPeriodId.Value))
                .GroupBy(d => d.PayrollPeriodId!.Value)
                .Select(g => new { PeriodId = g.Key, Total = g.Sum(d => d.Amount) })
                .ToDictionaryAsync(x => x.PeriodId, x => x.Total, cancellationToken);

        var items = periods
            .Select(p =>
            {
                var agg = lineAggregates.GetValueOrDefault(p.Id, (Count: 0, Gross: 0m));
                var deductions = deductionsByPeriod.GetValueOrDefault(p.Id, 0m);
                return new AdminPayrollPeriodListItem(
                    p.Id,
                    p.StartDate,
                    p.EndDate,
                    p.CutoffDate,
                    p.PaymentDate,
                    p.Status.ToString(),
                    p.CreatedAtUtc,
                    p.ClosedAtUtc,
                    agg.Count,
                    agg.Gross,
                    agg.Gross - deductions);
            })
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
        var subtotalLookup = await BuildServiceSubtotalLookupAsync(lines, cancellationToken);

        // Deductions are period-level per nurse, subtracted once — not summed across service lines.
        var deductionsByNurse = await _dbContext.DeductionRecords
            .AsNoTracking()
            .Where(d => d.PayrollPeriodId == periodId)
            .GroupBy(d => d.NurseUserId)
            .Select(g => new { NurseUserId = g.Key, Total = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(x => x.NurseUserId, x => x.Total, cancellationToken);

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
                l.ServiceExecutionId.HasValue ? subtotalLookup.GetValueOrDefault(l.ServiceExecutionId.Value, 0m) : 0m,
                l.CreatedAtUtc))
            .ToList()
            .AsReadOnly();

        var staffSummary = lines
            .GroupBy(l => l.NurseUserId)
            .Select(g =>
            {
                var gross = g.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal);
                var deductions = deductionsByNurse.GetValueOrDefault(g.Key, 0m);
                return new AdminPayrollStaffSummary(
                    g.Key,
                    nurseLookup.GetValueOrDefault(g.Key, g.Key.ToString()),
                    g.Count(),
                    gross,
                    g.Sum(l => l.TransportIncentive),
                    g.Sum(l => l.AdjustmentsTotal),
                    deductions,
                    gross - deductions);
            })
            .OrderByDescending(s => s.NetCompensation)
            .ToList()
            .AsReadOnly();

        // Editable/deletable only while Open with no lines and no deductions
        // (installments are deductions too). Mirrors PeriodHasActivityAsync so the
        // UI can hide edit/delete instead of letting the action fail server-side.
        var canModify = !period.IsClosed && lines.Count == 0 && deductionsByNurse.Count == 0;

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
            staffSummary,
            canModify,
            period.ReopenedAtUtc,
            period.ReopenReason,
            period.ReopenCount);
    }

    public async Task<Guid> CreatePeriodAsync(
        DateOnly startDate,
        DateOnly endDate,
        DateOnly cutoffDate,
        DateOnly paymentDate,
        CancellationToken cancellationToken)
    {
        var clash = await FindOverlappingPeriodAsync(startDate, endDate, null, cancellationToken);
        if (clash is not null)
        {
            throw new ArgumentException(
                $"El período {startDate:dd/MM/yyyy}–{endDate:dd/MM/yyyy} se solapa con un período existente ({clash.StartDate:dd/MM/yyyy}–{clash.EndDate:dd/MM/yyyy}).");
        }

        var period = PayrollPeriod.Create(startDate, endDate, cutoffDate, paymentDate, DateTime.UtcNow);
        _dbContext.PayrollPeriods.Add(period);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return period.Id;
    }

    // Two date ranges overlap when each starts on or before the other ends.
    private Task<PayrollPeriod?> FindOverlappingPeriodAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludePeriodId,
        CancellationToken cancellationToken)
        => _dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(p => (excludePeriodId == null || p.Id != excludePeriodId)
                        && p.StartDate <= endDate
                        && startDate <= p.EndDate)
            .OrderBy(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PeriodCloseResult> ClosePeriodAsync(
        Guid periodId,
        bool acknowledgeWarnings,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return PeriodCloseResult.NotFound;

        // Closing is idempotent — re-closing a closed period is a no-op success.
        if (period.IsClosed) return PeriodCloseResult.Success;

        // A period with zero values (no calculated lines and no deductions) has nothing
        // to settle and must not be closed.
        if (!await PeriodHasActivityAsync(periodId, cancellationToken))
        {
            return PeriodCloseResult.Empty;
        }

        // Safe-close gate, re-evaluated INSIDE the close path (not from a stale preflight).
        // The controller's pre-close advisory call can race the actual close: unliquidated
        // services or zero/negative net pay may appear (or be acknowledged against different
        // data) between the preflight and now. Re-checking here makes the gate authoritative
        // at close time and TOCTOU-safe — closing is irreversible-by-default, so an
        // unacknowledged warning must block the lock regardless of what the preflight saw.
        if (!acknowledgeWarnings)
        {
            var warnings = await GetCloseWarningsAsync(periodId, cancellationToken);
            if (warnings.HasWarnings)
            {
                return PeriodCloseResult.RequiresConfirmation;
            }
        }

        period.Close(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return PeriodCloseResult.Success;
    }

    public async Task<PeriodCloseWarnings> GetCloseWarningsAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return new PeriodCloseWarnings(0, 0);

        // Nurses whose net pay (gross − period deductions) is zero or negative.
        var grossByNurse = await _dbContext.PayrollLines
            .AsNoTracking()
            .Where(l => l.PayrollPeriodId == periodId)
            .GroupBy(l => l.NurseUserId)
            .Select(g => new
            {
                NurseUserId = g.Key,
                Gross = g.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal),
            })
            .ToListAsync(cancellationToken);

        var deductionsByNurse = await _dbContext.DeductionRecords
            .AsNoTracking()
            .Where(d => d.PayrollPeriodId == periodId)
            .GroupBy(d => d.NurseUserId)
            .Select(g => new { NurseUserId = g.Key, Total = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(x => x.NurseUserId, x => x.Total, cancellationToken);

        var negativeNetNurses = grossByNurse
            .Count(n => n.Gross - deductionsByNurse.GetValueOrDefault(n.NurseUserId, 0m) <= 0m);

        // Completed service executions inside the window with no payroll line in this period.
        var executionsInWindow = await _dbContext.ServiceExecutions
            .AsNoTracking()
            .Where(e => e.ServiceDate >= period.StartDate && e.ServiceDate <= period.EndDate)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var unliquidatedServices = 0;
        if (executionsInWindow.Count > 0)
        {
            var postedExecutionIds = await _dbContext.PayrollLines
                .AsNoTracking()
                .Where(l => l.PayrollPeriodId == periodId && l.ServiceExecutionId != null)
                .Select(l => l.ServiceExecutionId!.Value)
                .ToListAsync(cancellationToken);

            unliquidatedServices = executionsInWindow.Except(postedExecutionIds).Count();
        }

        return new PeriodCloseWarnings(negativeNetNurses, unliquidatedServices);
    }

    public async Task<PeriodMutationResult> UpdatePeriodAsync(
        Guid periodId,
        DateOnly startDate,
        DateOnly endDate,
        DateOnly cutoffDate,
        DateOnly paymentDate,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return PeriodMutationResult.NotFound;
        if (period.IsClosed) return PeriodMutationResult.Closed;
        if (await PeriodHasActivityAsync(periodId, cancellationToken)) return PeriodMutationResult.InUse;
        if (await FindOverlappingPeriodAsync(startDate, endDate, periodId, cancellationToken) is not null)
            return PeriodMutationResult.Overlap;

        period.UpdateSchedule(startDate, endDate, cutoffDate, paymentDate);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return PeriodMutationResult.Success;
    }

    public async Task<PeriodMutationResult> DeletePeriodAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null) return PeriodMutationResult.NotFound;
        if (period.IsClosed) return PeriodMutationResult.Closed;
        if (await PeriodHasActivityAsync(periodId, cancellationToken)) return PeriodMutationResult.InUse;

        _dbContext.PayrollPeriods.Remove(period);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return PeriodMutationResult.Success;
    }

    // A period is "in use" once payroll has been calculated for it (lines) or any
    // deduction/scheduled-deduction installment targets it. Installments are stored as
    // DeductionRecords carrying the period id, so the deduction check covers them too.
    private async Task<bool> PeriodHasActivityAsync(Guid periodId, CancellationToken cancellationToken)
    {
        if (await _dbContext.PayrollLines.AnyAsync(l => l.PayrollPeriodId == periodId, cancellationToken))
        {
            return true;
        }

        return await _dbContext.DeductionRecords.AnyAsync(d => d.PayrollPeriodId == periodId, cancellationToken);
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
        var subtotalLookup = await BuildServiceSubtotalLookupAsync(lines, cancellationToken);

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
                l.ServiceExecutionId.HasValue ? subtotalLookup.GetValueOrDefault(l.ServiceExecutionId.Value, 0m) : 0m,
                l.CreatedAtUtc))
            .ToList()
            .AsReadOnly();
    }

    private async Task<Dictionary<Guid, decimal>> BuildServiceSubtotalLookupAsync(
        IReadOnlyCollection<NursingCareBackend.Domain.Payroll.PayrollLine> lines,
        CancellationToken cancellationToken)
    {
        var executionIds = lines
            .Where(l => l.ServiceExecutionId.HasValue)
            .Select(l => l.ServiceExecutionId!.Value)
            .Distinct()
            .ToList();

        if (executionIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        return await _dbContext.ServiceExecutions
            .AsNoTracking()
            .Where(e => executionIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.SubtotalBeforeSupplies, cancellationToken);
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
        // One-time deductions only. Installments generated by a scheduled plan
        // (ScheduledDeductionId != null) are managed under "Deducciones Programadas",
        // not here — a multi-payment item is not a one-time deduction.
        var query = _dbContext.DeductionRecords
            .AsNoTracking()
            .Where(d => d.ScheduledDeductionId == null);

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
        // Payroll immutability guard: enforce via domain method (ADR-005)
        if (request.PayrollPeriodId != Guid.Empty)
        {
            var period = await _dbContext.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PayrollPeriodId, cancellationToken);

            if (period is not null)
            {
                try { period.EnsureOpen(); }
                catch (InvalidOperationException) { throw new PayrollPeriodClosedException(period.Id); }
            }
        }

        if (!Enum.TryParse<DeductionType>(request.DeductionType, ignoreCase: true, out var deductionType))
            throw new ArgumentException($"Tipo de deducción invalido: {request.DeductionType}");

        // A one-time deduction is a single payment. Labels that encode installments
        // ("cuota 2 de 6", "1/4", ...) belong to a scheduled plan — route them there.
        if (LooksLikeInstallment(request.Label))
            throw new ArgumentException(
                "Para descuentos en cuotas (préstamos a plazos, etc.) usa Descuentos Fijos. " +
                "Esta pantalla es solo para descuentos de un solo pago.");

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

    public async Task<bool> UpdateDeductionAsync(Guid deductionId, UpdateDeductionRequest request, CancellationToken cancellationToken)
    {
        var deduction = await _dbContext.DeductionRecords
            .FirstOrDefaultAsync(d => d.Id == deductionId, cancellationToken);

        if (deduction is null) return false;

        // Generated installments belong to a scheduled deduction; edit the plan, not the cuota.
        if (deduction.ScheduledDeductionId is not null)
        {
            throw new InvalidOperationException(
                "No se puede editar una cuota generada automáticamente; gestiona el descuento fijo.");
        }

        // Payroll immutability guard: can't edit a deduction inside a closed period.
        if (deduction.PayrollPeriodId != Guid.Empty)
        {
            var period = await _dbContext.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == deduction.PayrollPeriodId, cancellationToken);

            if (period is not null)
            {
                try { period.EnsureOpen(); }
                catch (InvalidOperationException) { throw new PayrollPeriodClosedException(period.Id); }
            }
        }

        if (!Enum.TryParse<DeductionType>(request.DeductionType, ignoreCase: true, out var deductionType))
        {
            throw new ArgumentException($"Tipo de deducción invalido: {request.DeductionType}");
        }

        if (LooksLikeInstallment(request.Label))
            throw new ArgumentException(
                "Para descuentos en cuotas (préstamos a plazos, etc.) usa Descuentos Fijos. " +
                "Esta pantalla es solo para descuentos de un solo pago.");

        deduction.Update(deductionType, request.Label, request.Amount, deduction.Notes);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Detects labels that encode an installment plan, e.g. "cuota 2 de 6", "1/4".
    // Such deductions are not one-time and must be created as scheduled plans.
    private static bool LooksLikeInstallment(string? label)
        => !string.IsNullOrWhiteSpace(label)
            && System.Text.RegularExpressions.Regex.IsMatch(
                label,
                @"\bcuotas?\b|\d+\s*/\s*\d+|\b\d+\s+de\s+\d+\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public async Task<bool> DeleteDeductionAsync(Guid deductionId, CancellationToken cancellationToken)
    {
        var deduction = await _dbContext.DeductionRecords
            .FirstOrDefaultAsync(d => d.Id == deductionId, cancellationToken);

        if (deduction is null) return false;

        // Payroll immutability guard: enforce via domain method (ADR-005)
        if (deduction.PayrollPeriodId != Guid.Empty)
        {
            var period = await _dbContext.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == deduction.PayrollPeriodId, cancellationToken);

            if (period is not null)
            {
                try { period.EnsureOpen(); }
                catch (InvalidOperationException) { throw new PayrollPeriodClosedException(period.Id); }
            }
        }

        _dbContext.DeductionRecords.Remove(deduction);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetDeductionPausedAsync(Guid deductionId, bool paused, CancellationToken cancellationToken)
    {
        var deduction = await _dbContext.DeductionRecords
            .FirstOrDefaultAsync(d => d.Id == deductionId, cancellationToken);

        if (deduction is null) return false;

        if (deduction.ScheduledDeductionId is null)
        {
            throw new InvalidOperationException("Solo se pueden pausar las cuotas de un descuento programado.");
        }

        // A cuota can only be paused/resumed while its period is still open.
        if (deduction.PayrollPeriodId is { } periodId && periodId != Guid.Empty)
        {
            var period = await _dbContext.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

            if (period is not null)
            {
                try { period.EnsureOpen(); }
                catch (InvalidOperationException) { throw new PayrollPeriodClosedException(period.Id); }
            }
        }

        if (paused) deduction.Pause();
        else deduction.Resume();

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
        await RecomputeAdjustmentsForServiceExecutionAsync(request.ServiceExecutionId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return adjustment.Id;
    }

    public async Task<bool> UpdateAdjustmentAsync(Guid adjustmentId, UpdateCompensationAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = await _dbContext.CompensationAdjustments
            .FirstOrDefaultAsync(a => a.Id == adjustmentId, cancellationToken);

        if (adjustment is null) return false;

        adjustment.Update(request.Label, request.Amount, null);
        var serviceExecutionId = adjustment.ServiceExecutionId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecomputeAdjustmentsForServiceExecutionAsync(serviceExecutionId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAdjustmentAsync(Guid adjustmentId, CancellationToken cancellationToken)
    {
        var adjustment = await _dbContext.CompensationAdjustments
            .FirstOrDefaultAsync(a => a.Id == adjustmentId, cancellationToken);

        if (adjustment is null) return false;

        var serviceExecutionId = adjustment.ServiceExecutionId;
        _dbContext.CompensationAdjustments.Remove(adjustment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecomputeAdjustmentsForServiceExecutionAsync(serviceExecutionId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Fold the current CompensationAdjustment total for a service execution into both the
    // ServiceExecution (drives payment reports) and the PayrollLine (drives the voucher/period
    // detail). Skips lines in a CLOSED period (payroll immutability). Caller must SaveChanges.
    private async Task RecomputeAdjustmentsForServiceExecutionAsync(Guid serviceExecutionId, CancellationToken cancellationToken)
    {
        var total = await _dbContext.CompensationAdjustments
            .Where(a => a.ServiceExecutionId == serviceExecutionId)
            .SumAsync(a => a.Amount, cancellationToken);

        var line = await _dbContext.PayrollLines
            .FirstOrDefaultAsync(l => l.ServiceExecutionId == serviceExecutionId, cancellationToken);
        if (line is not null)
        {
            var period = await _dbContext.PayrollPeriods.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == line.PayrollPeriodId, cancellationToken);
            if (period is not null && period.Status != PayrollPeriodStatus.Open)
            {
                return; // closed period: do not alter historical pay
            }
            line.SetAdjustmentsTotal(total, DateTime.UtcNow);
        }

        var execution = await _dbContext.ServiceExecutions
            .FirstOrDefaultAsync(s => s.Id == serviceExecutionId, cancellationToken);
        execution?.SetAdjustmentsTotal(total, DateTime.UtcNow);
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
                // Deduction-free compensation; period deductions are subtracted once below.
                GrossNet = g.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal)
            })
            .ToListAsync(cancellationToken);

        var periodIds = linesByPeriod.Select(x => x.PeriodId).ToList();

        var deductionsByPeriod = await _dbContext.DeductionRecords
            .AsNoTracking()
            .Where(d => d.NurseUserId == nurseId && d.PayrollPeriodId != null && periodIds.Contains(d.PayrollPeriodId.Value))
            .GroupBy(d => d.PayrollPeriodId!.Value)
            .Select(g => new { PeriodId = g.Key, Total = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(x => x.PeriodId, x => x.Total, cancellationToken);

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
                var deductions = deductionsByPeriod.GetValueOrDefault(p.Id, 0m);
                return new NursePeriodHistoryItem(
                    p.Id,
                    p.StartDate,
                    p.EndDate,
                    p.Status.ToString(),
                    summary?.ServiceCount ?? 0,
                    (summary?.GrossNet ?? 0m) - deductions);
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

        var periodDeductions = await _dbContext.DeductionRecords
            .AsNoTracking()
            .Where(d => d.NurseUserId == nurseId && d.PayrollPeriodId == periodId)
            .SumAsync(d => (decimal?)d.Amount, cancellationToken) ?? 0m;

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
            periodDeductions,
            lines.Sum(l => l.AdjustmentsTotal),
            lines.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal) - periodDeductions,
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
            TotalDeductions = deductions.Sum(d => d.Amount),
            NetCompensation = lines.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal) - deductions.Sum(d => d.Amount),
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
                TotalDeductions = nurseDeductions.Sum(d => d.Amount),
                NetCompensation = lines.Sum(l => l.BaseCompensation + l.TransportIncentive + l.ComplexityBonus + l.MedicalSuppliesCompensation + l.AdjustmentsTotal) - nurseDeductions.Sum(d => d.Amount),
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

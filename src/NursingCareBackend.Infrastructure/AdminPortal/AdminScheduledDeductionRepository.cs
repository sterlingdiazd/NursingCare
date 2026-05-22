using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminScheduledDeductionRepository : IAdminScheduledDeductionRepository
{
    private readonly NursingCareDbContext _db;

    public AdminScheduledDeductionRepository(NursingCareDbContext db)
    {
        _db = db;
    }

    public async Task<ScheduledDeductionListResult> GetAsync(Guid? nurseId, string? status, CancellationToken cancellationToken)
    {
        var query = _db.ScheduledDeductions.AsNoTracking();

        if (nurseId.HasValue)
            query = query.Where(s => s.NurseUserId == nurseId.Value);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ScheduledDeductionStatus>(status, ignoreCase: true, out var parsed))
            query = query.Where(s => s.Status == parsed);

        var plans = await query.OrderByDescending(s => s.CreatedAtUtc).ToListAsync(cancellationToken);
        var names = await BuildNurseLookupAsync(plans.Select(p => p.NurseUserId).Distinct().ToList(), cancellationToken);

        var items = plans.Select(p => ToListItem(p, names)).ToList().AsReadOnly();
        return new ScheduledDeductionListResult(items, items.Count);
    }

    public async Task<ScheduledDeductionDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _db.ScheduledDeductions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (plan is null) return null;

        var names = await BuildNurseLookupAsync(new[] { plan.NurseUserId }, cancellationToken);

        var records = await _db.DeductionRecords.AsNoTracking()
            .Where(d => d.ScheduledDeductionId == id)
            .OrderBy(d => d.InstallmentSequence)
            .ToListAsync(cancellationToken);

        var periodIds = records.Where(r => r.PayrollPeriodId.HasValue).Select(r => r.PayrollPeriodId!.Value).Distinct().ToList();
        var periods = await _db.PayrollPeriods.AsNoTracking()
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var rows = records.Select(r =>
        {
            PayrollPeriod? period = r.PayrollPeriodId.HasValue && periods.TryGetValue(r.PayrollPeriodId.Value, out var p) ? p : null;
            return new ScheduledDeductionInstallmentRow(
                r.InstallmentSequence,
                r.PayrollPeriodId,
                period?.StartDate,
                period?.EndDate,
                r.Label,
                r.Amount,
                period is { Status: PayrollPeriodStatus.Closed });
        }).ToList().AsReadOnly();

        return new ScheduledDeductionDetail(ToListItem(plan, names), rows);
    }

    public async Task<Guid> CreateAsync(CreateScheduledDeductionRequest request, Guid createdByUserId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DeductionType>(request.DeductionType, ignoreCase: true, out var type))
            throw new ArgumentException($"Tipo de deducción inválido: '{request.DeductionType}'.");
        if (!Enum.TryParse<ScheduleModality>(request.Modality, ignoreCase: true, out var modality))
            throw new ArgumentException($"Modalidad inválida: '{request.Modality}'.");
        if (!Enum.TryParse<DeductionCadence>(request.Cadence, ignoreCase: true, out var cadence))
            throw new ArgumentException($"Frecuencia inválida: '{request.Cadence}'.");

        var now = DateTime.UtcNow;
        ScheduledDeduction plan;

        switch (modality)
        {
            case ScheduleModality.Amortizing:
                if (request.PrincipalAmount is not { } principal)
                    throw new ArgumentException("El capital es obligatorio para un plan amortizable.");
                if (request.TotalInstallments is not { } installments)
                    throw new ArgumentException("La cantidad de cuotas es obligatoria.");
                plan = ScheduledDeduction.CreateAmortizing(
                    request.NurseUserId, type, request.Label, principal,
                    request.InterestRatePercent ?? 0m, installments, cadence,
                    request.StartPeriodDate, request.Notes, createdByUserId, now);
                break;

            case ScheduleModality.RecurringFixed:
                if (request.RecurringAmount is not { } recurring)
                    throw new ArgumentException("El monto recurrente es obligatorio.");
                plan = ScheduledDeduction.CreateRecurring(
                    request.NurseUserId, type, request.Label, recurring, cadence,
                    request.StartPeriodDate, request.EndDate, request.MaxOccurrences,
                    request.Notes, createdByUserId, now);
                break;

            default:
                throw new ArgumentException("La modalidad 'Una sola vez' usa el endpoint de deducciones.");
        }

        _db.ScheduledDeductions.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }

    public async Task<bool> PayoffAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _db.ScheduledDeductions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (plan is null) return false;

        var remaining = plan.EnsureEarlyPayoffAllowed();

        var openPeriodIds = await _db.PayrollPeriods
            .Where(p => p.Status == PayrollPeriodStatus.Open)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // The payoff installment replaces any pending (open-period) installments.
        var pending = await _db.DeductionRecords
            .Where(d => d.ScheduledDeductionId == id && d.PayrollPeriodId != null && openPeriodIds.Contains(d.PayrollPeriodId.Value))
            .ToListAsync(cancellationToken);
        _db.DeductionRecords.RemoveRange(pending);

        var target = await _db.PayrollPeriods
            .Where(p => p.Status == PayrollPeriodStatus.Open && p.StartDate >= plan.StartPeriodDate)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (target is null)
            throw new InvalidOperationException("No hay un período abierto para registrar la liquidación.");

        var closedPeriodIds = await _db.PayrollPeriods
            .Where(p => p.Status == PayrollPeriodStatus.Closed)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var paidCount = await _db.DeductionRecords.CountAsync(
            d => d.ScheduledDeductionId == id && d.PayrollPeriodId != null && closedPeriodIds.Contains(d.PayrollPeriodId.Value),
            cancellationToken);

        _db.DeductionRecords.Add(DeductionRecord.Create(
            nurseUserId: plan.NurseUserId,
            payrollPeriodId: target.Id,
            deductionType: plan.DeductionType,
            label: $"{plan.Label} · liquidación anticipada",
            amount: remaining,
            notes: "Liquidación anticipada del saldo pendiente.",
            effectiveAtUtc: target.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            createdAtUtc: DateTime.UtcNow,
            scheduledDeductionId: plan.Id,
            installmentSequence: paidCount + 1));

        plan.SyncGeneratedCount(paidCount + 1);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RescheduleAsync(Guid id, RescheduleScheduledDeductionRequest request, CancellationToken cancellationToken)
    {
        var plan = await _db.ScheduledDeductions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (plan is null) return false;

        var now = DateTime.UtcNow;
        if (plan.Modality == ScheduleModality.Amortizing)
        {
            if (request.InstallmentAmount is not { } amount)
                throw new ArgumentException("El monto de cuota es obligatorio para reprogramar un plan amortizable.");
            plan.RescheduleAmortizing(amount, now);
        }
        else
        {
            plan.RescheduleRecurring(request.RecurringAmount, request.EndDate, request.MaxOccurrences, now);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SkipInstallmentAsync(Guid id, Guid payrollPeriodId, CancellationToken cancellationToken)
    {
        var plan = await _db.ScheduledDeductions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (plan is null) return false;

        var period = await _db.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == payrollPeriodId, cancellationToken);
        if (period is null)
            throw new InvalidOperationException("Período no encontrado.");
        if (period.IsClosed)
            throw new InvalidOperationException("No se puede omitir una cuota de un período cerrado.");

        var record = await _db.DeductionRecords
            .FirstOrDefaultAsync(d => d.ScheduledDeductionId == id && d.PayrollPeriodId == payrollPeriodId, cancellationToken);
        if (record is null)
            throw new InvalidOperationException("No hay cuota pendiente para omitir en este período.");

        record.MarkSkipped();
        plan.RegisterSkip();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelAsync(Guid id, string reason, Guid cancelledByUserId, CancellationToken cancellationToken)
    {
        var plan = await _db.ScheduledDeductions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (plan is null) return false;

        plan.Cancel(cancelledByUserId, reason, DateTime.UtcNow);

        // Stop deducting: drop any pending (open-period) installments.
        var openPeriodIds = await _db.PayrollPeriods
            .Where(p => p.Status == PayrollPeriodStatus.Open)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var pending = await _db.DeductionRecords
            .Where(d => d.ScheduledDeductionId == id && d.PayrollPeriodId != null && openPeriodIds.Contains(d.PayrollPeriodId.Value))
            .ToListAsync(cancellationToken);
        _db.DeductionRecords.RemoveRange(pending);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ScheduledDeductionListItem ToListItem(ScheduledDeduction p, IReadOnlyDictionary<Guid, string> names) =>
        new(
            p.Id,
            p.NurseUserId,
            names.GetValueOrDefault(p.NurseUserId, p.NurseUserId.ToString()),
            p.DeductionType.ToString(),
            p.Label,
            p.Modality.ToString(),
            p.Cadence.ToString(),
            p.Status.ToString(),
            p.StartPeriodDate,
            p.PrincipalAmount,
            p.InterestRatePercent,
            p.TotalRepayable,
            p.InstallmentAmount,
            p.TotalInstallments,
            p.RecurringAmount,
            p.EndDate,
            p.MaxOccurrences,
            p.InstallmentsGenerated,
            p.InstallmentsPaid,
            p.AmountSettled,
            p.RemainingBalance,
            p.Notes,
            p.CreatedAtUtc,
            p.ClosedAtUtc);

    private async Task<Dictionary<Guid, string>> BuildNurseLookupAsync(IReadOnlyCollection<Guid> nurseIds, CancellationToken cancellationToken)
    {
        if (nurseIds.Count == 0) return new Dictionary<Guid, string>();

        var users = await _db.Users.AsNoTracking()
            .Where(u => nurseIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.LastName, u.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id, u =>
        {
            var full = string.Join(" ", new[] { u.Name, u.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(full) ? u.Email : full;
        });
    }
}

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminShiftRepository : IAdminShiftRepository
{
    private readonly NursingCareDbContext _dbContext;

    public AdminShiftRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminShiftListResult> GetShiftsAsync(AdminShiftListFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.ShiftRecords.AsNoTracking();

        if (filter.NurseUserId.HasValue)
            query = query.Where(s => s.NurseUserId == filter.NurseUserId.Value);
        if (filter.CareRequestId.HasValue)
            query = query.Where(s => s.CareRequestId == filter.CareRequestId.Value);
        if (filter.StartDate.HasValue)
        {
            var startDateTime = filter.StartDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(s => s.ScheduledStartUtc >= startDateTime);
        }
        if (filter.EndDate.HasValue)
        {
            var endDateTime = filter.EndDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(s => s.ScheduledStartUtc <= endDateTime);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) 
            && Enum.TryParse<ShiftRecordStatus>(filter.Status, ignoreCase: true, out var status))
            query = query.Where(s => s.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var shifts = await query
            .OrderByDescending(s => s.ScheduledStartUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var nurseIds = shifts.Where(s => s.NurseUserId.HasValue).Select(s => s.NurseUserId!.Value).Distinct().ToList();
        var nurseLookup = await BuildNurseLookupAsync(nurseIds, cancellationToken);

        var items = shifts
            .Select(s => new AdminShiftRecordListItem(
                s.Id,
                s.CareRequestId,
                s.NurseUserId ?? Guid.Empty,
                s.NurseUserId.HasValue 
                    ? nurseLookup.GetValueOrDefault(s.NurseUserId.Value, s.NurseUserId.Value.ToString()) 
                    : "Sin asignar",
                s.ScheduledStartUtc?.ToString("o") ?? "",
                s.ScheduledEndUtc?.ToString("o"),
                s.Status.ToString(),
                s.CreatedAtUtc))
            .ToList()
            .AsReadOnly();

        return new AdminShiftListResult(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<AdminShiftRecordDetail?> GetShiftByIdAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await _dbContext.ShiftRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

        if (shift is null) return null;

        var nurseLookup = shift.NurseUserId.HasValue
            ? await BuildNurseLookupAsync(new[] { shift.NurseUserId!.Value }, cancellationToken)
            : new Dictionary<Guid, string>();
        
        var changes = await GetShiftChangesAsync(shiftId, cancellationToken);

        return new AdminShiftRecordDetail(
            shift.Id,
            shift.CareRequestId,
            shift.NurseUserId ?? Guid.Empty,
            shift.NurseUserId.HasValue
                ? nurseLookup.GetValueOrDefault(shift.NurseUserId.Value, shift.NurseUserId.Value.ToString())
                : "Sin asignar",
            shift.ScheduledStartUtc?.ToString("o") ?? "",
            shift.ScheduledEndUtc?.ToString("o"),
            shift.Status.ToString(),
            shift.CreatedAtUtc,
            changes);
    }

    public async Task<IReadOnlyList<AdminShiftChangeItem>> GetShiftChangesAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var changes = await _dbContext.ShiftChanges
            .AsNoTracking()
            .Where(c => c.ShiftRecordId == shiftId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var allNurseIds = changes
            .SelectMany(c => new[] { c.PreviousNurseUserId, c.NewNurseUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var nurseLookup = allNurseIds.Count > 0 
            ? await BuildNurseLookupAsync(allNurseIds, cancellationToken)
            : new Dictionary<Guid, string>();

        return changes
            .Select(c => new AdminShiftChangeItem(
                c.Id,
                c.PreviousNurseUserId,
                c.PreviousNurseUserId.HasValue 
                    ? nurseLookup.GetValueOrDefault(c.PreviousNurseUserId.Value, c.PreviousNurseUserId.Value.ToString()) 
                    : null,
                c.NewNurseUserId,
                c.NewNurseUserId.HasValue 
                    ? nurseLookup.GetValueOrDefault(c.NewNurseUserId.Value, c.NewNurseUserId.Value.ToString()) 
                    : null,
                c.Reason,
                c.EffectiveAtUtc,
                c.CreatedAtUtc))
            .ToList()
            .AsReadOnly();
    }

    private async Task<Dictionary<Guid, string>> BuildNurseLookupAsync(IReadOnlyCollection<Guid> nurseIds, CancellationToken cancellationToken)
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
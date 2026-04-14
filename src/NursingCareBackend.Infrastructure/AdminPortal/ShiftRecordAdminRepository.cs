using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Shifts;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class ShiftRecordAdminRepository : IShiftRecordAdminRepository
{
  private readonly NursingCareDbContext _dbContext;

  public ShiftRecordAdminRepository(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<Guid> RegisterShiftAsync(
    Guid careRequestId,
    Guid? nurseUserId,
    DateTime? scheduledStartUtc,
    DateTime? scheduledEndUtc,
    CancellationToken cancellationToken)
  {
    var exists = await _dbContext.CareRequests
      .AnyAsync(c => c.Id == careRequestId, cancellationToken);

    if (!exists)
    {
      throw new KeyNotFoundException($"Care request '{careRequestId}' was not found.");
    }

    if (scheduledStartUtc.HasValue
      && scheduledEndUtc.HasValue
      && scheduledStartUtc.Value > scheduledEndUtc.Value)
    {
      throw new ArgumentException("Scheduled start must be before or equal to scheduled end.");
    }

    var utcNow = DateTime.UtcNow;
    var shift = ShiftRecord.Create(careRequestId, nurseUserId, scheduledStartUtc, scheduledEndUtc, utcNow);
    _dbContext.ShiftRecords.Add(shift);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return shift.Id;
  }

  public async Task RecordShiftChangeAsync(
    Guid careRequestId,
    Guid shiftRecordId,
    Guid? newNurseUserId,
    string reason,
    DateTime effectiveAtUtc,
    CancellationToken cancellationToken)
  {
    var shift = await _dbContext.ShiftRecords
      .FirstOrDefaultAsync(
        s => s.Id == shiftRecordId && s.CareRequestId == careRequestId,
        cancellationToken);

    if (shift is null)
    {
      throw new KeyNotFoundException($"Shift '{shiftRecordId}' was not found for this care request.");
    }

    if (shift.Status == ShiftRecordStatus.Completed)
    {
      throw new InvalidOperationException("Completed shifts cannot be changed.");
    }

    if (shift.NurseUserId == newNurseUserId)
    {
      throw new InvalidOperationException("The new nurse assignment must differ from the current one.");
    }

    var utcNow = DateTime.UtcNow;
    var change = ShiftChange.Create(
      shiftRecordId,
      shift.NurseUserId,
      newNurseUserId,
      reason,
      effectiveAtUtc,
      utcNow);

    _dbContext.ShiftChanges.Add(change);
    shift.Reassign(newNurseUserId, utcNow);

    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}

using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminPayrollOverrideRepository : IAdminPayrollOverrideRepository
{
    private readonly NursingCareDbContext _dbContext;

    public AdminPayrollOverrideRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> SubmitOverrideAsync(
        SubmitOverrideRequest request,
        Guid requestedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Validate line exists
        var lineExists = await _dbContext.PayrollLines
            .AnyAsync(l => l.Id == request.LineId, cancellationToken);

        if (!lineExists)
            throw new ArgumentException($"PayrollLine '{request.LineId}' not found.");

        // Cancel any existing pending override for this line
        var existingPending = await _dbContext.PayrollLineOverrides
            .Where(o => o.PayrollLineId == request.LineId && o.Status == PayrollLineOverrideStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var pending in existingPending)
            pending.Reject(now);

        var overrideEntity = PayrollLineOverride.Create(
            request.LineId,
            requestedByUserId,
            request.OverrideAmount,
            request.Reason,
            now);

        _dbContext.PayrollLineOverrides.Add(overrideEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return overrideEntity.Id;
    }

    public async Task<(bool Found, string? Error)> ApproveOverrideAsync(
        Guid lineId,
        Guid approvedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var overrideEntity = await _dbContext.PayrollLineOverrides
            .FirstOrDefaultAsync(o => o.PayrollLineId == lineId && o.Status == PayrollLineOverrideStatus.Pending, cancellationToken);

        if (overrideEntity is null)
            return (false, null);

        if (overrideEntity.RequestedByUserId == approvedByUserId)
            return (true, "El mismo administrador que solicitó la compensación manual no puede aprobarla.");

        var line = await _dbContext.PayrollLines
            .FirstOrDefaultAsync(l => l.Id == lineId, cancellationToken);

        if (line is null)
            return (false, null);

        try
        {
            overrideEntity.Approve(approvedByUserId, now);
            line.ApplyOverride(overrideEntity.OverrideAmount, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (InvalidOperationException ex)
        {
            return (true, ex.Message);
        }
    }

    public async Task<OverrideDetailDto?> GetOverrideForLineAsync(Guid lineId, CancellationToken cancellationToken)
    {
        var o = await _dbContext.PayrollLineOverrides
            .AsNoTracking()
            .Where(ov => ov.PayrollLineId == lineId)
            .OrderByDescending(ov => ov.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (o is null) return null;

        return new OverrideDetailDto(
            o.Id,
            o.PayrollLineId,
            o.RequestedByUserId,
            o.RequestedAtUtc,
            o.OverrideAmount,
            o.Reason,
            o.Status.ToString(),
            o.ApprovedByUserId,
            o.ResolvedAtUtc);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Reports;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminReportsRepository : IAdminReportsRepository
{
    private readonly NursingCareDbContext _dbContext;

    public AdminReportsRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private static IQueryable<CareRequest> FilterCareRequestsByDate(IQueryable<CareRequest> query, DateOnly? from, DateOnly? to)
    {
        if (from.HasValue)
        {
            var fromDateTime = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(cr => cr.CreatedAtUtc >= fromDateTime);
        }

        if (to.HasValue)
        {
            var toDateTime = to.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(cr => cr.CreatedAtUtc <= toDateTime);
        }

        return query;
    }

    public async Task<CareRequestPipelineReport> GetCareRequestPipelineReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = FilterCareRequestsByDate(_dbContext.CareRequests.AsNoTracking(), from, to);

        var pendingCount = await query.CountAsync(c => c.Status == CareRequestStatus.Pending, cancellationToken);
        var approvedCount = await query.CountAsync(c => c.Status == CareRequestStatus.Approved, cancellationToken);
        var completedCount = await query.CountAsync(c => c.Status == CareRequestStatus.Completed, cancellationToken);
        var rejectedCount = await query.CountAsync(c => c.Status == CareRequestStatus.Rejected, cancellationToken);
        var unassignedCount = await query.CountAsync(c => c.Status == CareRequestStatus.Pending && !c.AssignedNurse.HasValue, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var currentCareDate = DateOnly.FromDateTime(utcNow);
        var staleCutoffUtc = utcNow.AddHours(-48);

        var overdueCount = await query.CountAsync(careRequest =>
            careRequest.Status != CareRequestStatus.Completed
            && (
                (careRequest.CareRequestDate.HasValue && careRequest.CareRequestDate.Value < currentCareDate)
                || (!careRequest.CareRequestDate.HasValue
                    && careRequest.Status == CareRequestStatus.Pending
                    && careRequest.UpdatedAtUtc <= staleCutoffUtc)
            ), cancellationToken);

        return new CareRequestPipelineReport(
            PendingCount: pendingCount,
            ApprovedCount: approvedCount,
            CompletedCount: completedCount,
            RejectedCount: rejectedCount,
            UnassignedCount: unassignedCount,
            OverdueCount: overdueCount
        );
    }

    public async Task<AssignmentApprovalBacklogReport> GetAssignmentApprovalBacklogReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = FilterCareRequestsByDate(_dbContext.CareRequests.AsNoTracking(), from, to)
            .Where(c => c.Status == CareRequestStatus.Pending);

        var pendingUnassignedCount = await query.CountAsync(c => !c.AssignedNurse.HasValue, cancellationToken);
        var pendingAssignedAwaitingApprovalCount = await query.CountAsync(c => c.AssignedNurse.HasValue, cancellationToken);

        var pendingRequests = await query.Select(c => c.CreatedAtUtc).ToListAsync(cancellationToken);
        double averageDaysPending = 0;
        
        if (pendingRequests.Count > 0)
        {
            var now = DateTime.UtcNow;
            averageDaysPending = pendingRequests.Average(createdAt => (now - createdAt).TotalDays);
        }

        return new AssignmentApprovalBacklogReport(
            PendingUnassignedCount: pendingUnassignedCount,
            PendingAssignedAwaitingApprovalCount: pendingAssignedAwaitingApprovalCount,
            AverageDaysPending: averageDaysPending
        );
    }

    public async Task<NurseOnboardingReport> GetNurseOnboardingReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking()
            .Where(u => u.ProfileType == UserProfileType.NURSE);

        if (from.HasValue)
        {
            var fromDateTime = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(u => u.CreatedAtUtc >= fromDateTime);
        }

        if (to.HasValue)
        {
            var toDateTime = to.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(u => u.CreatedAtUtc <= toDateTime);
        }

        var totalRegistered = await query.CountAsync(cancellationToken);
        
        var pendingReview = await query.CountAsync(u => u.NurseProfile != null && !u.NurseProfile.IsActive, cancellationToken);
        var active = await query.CountAsync(u => u.IsActive && u.NurseProfile != null && u.NurseProfile.IsActive, cancellationToken);
        var inactive = await query.CountAsync(u => !u.IsActive, cancellationToken);

        // Completed this period can be derived from NurseProfile update time if it exists, or just approximated. Let's say if active.
        var completedThisPeriod = await query.CountAsync(u => u.NurseProfile != null && u.NurseProfile.IsActive, cancellationToken);

        return new NurseOnboardingReport(
            TotalRegisteredCount: totalRegistered,
            PendingReviewCount: pendingReview,
            ActiveCount: active,
            InactiveCount: inactive,
            CompletedThisPeriodCount: completedThisPeriod
        );
    }

    public async Task<ActiveInactiveUsersReport> GetActiveInactiveUsersReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var adminActive = await _dbContext.Users.AsNoTracking().CountAsync(u => u.ProfileType == UserProfileType.ADMIN && u.IsActive, cancellationToken);
        var adminInactive = await _dbContext.Users.AsNoTracking().CountAsync(u => u.ProfileType == UserProfileType.ADMIN && !u.IsActive, cancellationToken);

        var clientActive = await _dbContext.Users.AsNoTracking().CountAsync(u => u.ProfileType == UserProfileType.CLIENT && u.IsActive, cancellationToken);
        var clientInactive = await _dbContext.Users.AsNoTracking().CountAsync(u => u.ProfileType == UserProfileType.CLIENT && !u.IsActive, cancellationToken);

        var nurseActive = await _dbContext.Users.AsNoTracking().CountAsync(u => u.ProfileType == UserProfileType.NURSE && u.IsActive, cancellationToken);
        var nurseInactive = await _dbContext.Users.AsNoTracking().CountAsync(u => u.ProfileType == UserProfileType.NURSE && !u.IsActive, cancellationToken);

        return new ActiveInactiveUsersReport(
            AdminActiveCount: adminActive,
            AdminInactiveCount: adminInactive,
            ClientActiveCount: clientActive,
            ClientInactiveCount: clientInactive,
            NurseActiveCount: nurseActive,
            NurseInactiveCount: nurseInactive
        );
    }

    public async Task<NurseUtilizationReport> GetNurseUtilizationReportAsync(DateOnly? from, DateOnly? to, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var nurseQuery = _dbContext.Users.AsNoTracking()
            .Where(u => u.ProfileType == UserProfileType.NURSE && u.NurseProfile != null && u.NurseProfile.IsActive);

        var totalNurses = await nurseQuery.CountAsync(cancellationToken);

        var nurses = await nurseQuery
            .OrderBy(u => u.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, Name = u.Name + " " + u.LastName })
            .ToListAsync(cancellationToken);

        var nurseIds = nurses.Select(n => n.Id).ToList();

        var crQuery = FilterCareRequestsByDate(_dbContext.CareRequests.AsNoTracking(), from, to)
            .Where(cr => cr.AssignedNurse.HasValue && nurseIds.Contains(cr.AssignedNurse.Value));

        var careRequestStats = await crQuery
            .GroupBy(cr => cr.AssignedNurse!.Value)
            .Select(g => new
            {
                NurseId = g.Key,
                TotalAssigned = g.Count(),
                Completed = g.Count(c => c.Status == CareRequestStatus.Completed),
                Pending = g.Count(c => c.Status == CareRequestStatus.Pending)
            })
            .ToListAsync(cancellationToken);

        var rows = new List<NurseUtilizationRow>();
        foreach (var nurse in nurses)
        {
            var stats = careRequestStats.FirstOrDefault(s => s.NurseId == nurse.Id);
            int total = stats?.TotalAssigned ?? 0;
            int completed = stats?.Completed ?? 0;
            int pending = stats?.Pending ?? 0;
            double completionRate = total > 0 ? (double)completed / total : 0;

            rows.Add(new NurseUtilizationRow(
                NurseId: nurse.Id.ToString(),
                NurseName: nurse.Name,
                TotalAssigned: total,
                Completed: completed,
                Pending: pending,
                CompletionRate: completionRate
            ));
        }

        return new NurseUtilizationReport(
            Rows: rows,
            TotalNurses: totalNurses,
            PageNumber: pageNumber,
            PageSize: pageSize
        );
    }

    public async Task<CareRequestCompletionReport> GetCareRequestCompletionReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = FilterCareRequestsByDate(_dbContext.CareRequests.AsNoTracking(), from, to)
            .Where(c => c.Status == CareRequestStatus.Completed && c.CompletedAtUtc.HasValue);

        var totalCompleted = await query.CountAsync(cancellationToken);

        var completions = await query
            .Select(c => new { c.CreatedAtUtc, c.CompletedAtUtc })
            .ToListAsync(cancellationToken);

        double avgDays = 0;
        var map = new Dictionary<string, int>();

        if (completions.Count > 0)
        {
            avgDays = completions.Average(c => (c.CompletedAtUtc!.Value - c.CreatedAtUtc).TotalDays);

            foreach (var c in completions)
            {
                var monthKey = c.CompletedAtUtc!.Value.ToString("yyyy-MM");
                if (map.ContainsKey(monthKey))
                    map[monthKey]++;
                else
                    map[monthKey] = 1;
            }
        }

        return new CareRequestCompletionReport(
            TotalCompletedCount: totalCompleted,
            AverageDaysToComplete: avgDays,
            CompletionsByRange: map
        );
    }

    public async Task<PriceUsageSummaryReport> GetPriceUsageSummaryReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = FilterCareRequestsByDate(_dbContext.CareRequests.AsNoTracking(), from, to);

        var typeStats = await query
            .GroupBy(c => c.CareRequestType)
            .Select(g => new PriceUsageSummaryRow(
                g.Key,
                g.Count(),
                g.Average(c => c.Total),
                g.Sum(c => c.Total)
            ))
            .OrderByDescending(r => r.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var distanceFactors = await query
            .Where(c => c.DistanceFactor != null)
            .GroupBy(c => c.DistanceFactor)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key!)
            .Take(5)
            .ToListAsync(cancellationToken);

        var complexityLevels = await query
            .Where(c => c.ComplexityLevel != null)
            .GroupBy(c => c.ComplexityLevel)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key!)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new PriceUsageSummaryReport(
            TopRequestTypes: typeStats,
            TopDistanceFactors: distanceFactors,
            TopComplexityLevels: complexityLevels
        );
    }

    public async Task<NotificationVolumeReport> GetNotificationVolumeReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = _dbContext.AdminNotifications.AsNoTracking();

        if (from.HasValue)
        {
            var fromDateTime = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(n => n.CreatedAtUtc >= fromDateTime);
        }

        if (to.HasValue)
        {
            var toDateTime = to.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(n => n.CreatedAtUtc <= toDateTime);
        }

        var total = await query.CountAsync(cancellationToken);
        var unread = await query.CountAsync(n => n.ReadAtUtc == null && n.ArchivedAtUtc == null, cancellationToken);
        var byCategory = await query
            .GroupBy(n => n.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count, cancellationToken);

        // Pending action items: unread notifications roughly estimate but we don't have a separate ActionItem table, it's notifications
        int pendingActions = unread; 
        
        return new NotificationVolumeReport(
            TotalNotificationsCount: total,
            UnreadNotificationsCount: unread,
            NotificationsByCategory: byCategory,
            PendingActionItemsCount: pendingActions
        );
    }

    public async Task<PayrollSummaryReport> GetPayrollSummaryReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var periodStart = from ?? ResolveDefaultPayrollStart(DateOnly.FromDateTime(DateTime.UtcNow));
        var periodEnd = to ?? ResolveDefaultPayrollEnd(periodStart);

        var payrollPeriod = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(period => period.StartDate == periodStart && period.EndDate == periodEnd, cancellationToken);

        var executionQuery = _dbContext.ServiceExecutions
            .AsNoTracking()
            .Where(execution => execution.ServiceDate >= periodStart && execution.ServiceDate <= periodEnd);

        var executions = await executionQuery
            .OrderBy(execution => execution.ExecutedAtUtc)
            .ToListAsync(cancellationToken);

        var nurseIds = executions.Select(execution => execution.NurseUserId).Distinct().ToList();
        var nurses = await _dbContext.Users
            .AsNoTracking()
            .Where(user => nurseIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.Name,
                user.LastName,
                user.Email,
            })
            .ToListAsync(cancellationToken);

        var nurseLookup = nurses.ToDictionary(
            item => item.Id,
            item =>
            {
                var fullName = string.Join(" ", new[] { item.Name, item.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
                return string.IsNullOrWhiteSpace(fullName) ? item.Email : fullName;
            });

        var services = executions
            .Select(execution => new PayrollServiceRow(
                NurseId: execution.NurseUserId.ToString(),
                NurseName: nurseLookup.GetValueOrDefault(execution.NurseUserId, execution.NurseUserId.ToString()),
                CareRequestId: execution.CareRequestId.ToString(),
                CareRequestType: execution.CareRequestType,
                PricingCategoryCode: execution.PricingCategoryCode ?? "sin_categoria",
                EmploymentType: execution.EmploymentType.ToString(),
                ServiceVariant: execution.Variant.ToString(),
                ExecutedAtUtc: execution.ExecutedAtUtc,
                CareRequestTotal: execution.CareRequestTotal,
                BaseCompensation: execution.BaseCompensation,
                TransportIncentive: execution.TransportIncentive,
                ComplexityBonus: execution.ComplexityBonus,
                MedicalSuppliesCompensation: execution.MedicalSuppliesCompensation,
                AdjustmentsTotal: execution.AdjustmentsTotal,
                DeductionsTotal: execution.DeductionsTotal,
                NetCompensation: execution.NetCompensation))
            .ToList();

        var staff = services
            .GroupBy(row => new { row.NurseId, row.NurseName })
            .Select(group => new PayrollSummaryStaffRow(
                NurseId: group.Key.NurseId,
                NurseName: group.Key.NurseName,
                ServiceCount: group.Count(),
                GrossCompensation: group.Sum(row => row.BaseCompensation + row.TransportIncentive + row.ComplexityBonus + row.MedicalSuppliesCompensation + row.AdjustmentsTotal),
                TransportIncentives: group.Sum(row => row.TransportIncentive),
                AdjustmentsTotal: group.Sum(row => row.AdjustmentsTotal),
                DeductionsTotal: group.Sum(row => row.DeductionsTotal),
                NetCompensation: group.Sum(row => row.NetCompensation)))
            .OrderByDescending(row => row.NetCompensation)
            .ToList();

        var resolvedCutoffDate = payrollPeriod?.CutoffDate ?? periodEnd.AddDays(-2);
        var resolvedPaymentDate = payrollPeriod?.PaymentDate ?? periodEnd;
        var periodLabel = $"{periodStart:yyyy-MM-dd} al {periodEnd:yyyy-MM-dd}";

        return new PayrollSummaryReport(
            PeriodLabel: periodLabel,
            StartDate: periodStart,
            EndDate: periodEnd,
            CutoffDate: resolvedCutoffDate,
            PaymentDate: resolvedPaymentDate,
            Staff: staff,
            Services: services);
    }

    private static DateOnly ResolveDefaultPayrollStart(DateOnly today)
        => today.Day <= 15
            ? new DateOnly(today.Year, today.Month, 1)
            : new DateOnly(today.Year, today.Month, 16);

    private static DateOnly ResolveDefaultPayrollEnd(DateOnly periodStart)
        => periodStart.Day == 1
            ? new DateOnly(periodStart.Year, periodStart.Month, 15)
            : new DateOnly(periodStart.Year, periodStart.Month, DateTime.DaysInMonth(periodStart.Year, periodStart.Month));
}

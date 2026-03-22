using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminDashboardRepository : IAdminDashboardRepository
{
  private static readonly IReadOnlyList<AdminDashboardAlert> PlaceholderAlerts = Array.Empty<AdminDashboardAlert>();

  private readonly NursingCareDbContext _dbContext;

  public AdminDashboardRepository(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<AdminDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
  {
    var utcNow = DateTime.UtcNow;
    var utcToday = utcNow.Date;
    var currentCareDate = DateOnly.FromDateTime(utcNow);
    var staleCutoffUtc = utcNow.AddHours(-48);

    var pendingNurseProfilesCount = await _dbContext.Users
      .AsNoTracking()
      .Where(user =>
        user.ProfileType == UserProfileType.Nurse
        && user.NurseProfile != null
        && !user.NurseProfile.IsActive)
      .CountAsync(cancellationToken);

    var waitingForAssignmentCount = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest =>
        careRequest.Status == CareRequestStatus.Pending
        && !careRequest.AssignedNurse.HasValue)
      .CountAsync(cancellationToken);

    var waitingForApprovalCount = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest =>
        careRequest.Status == CareRequestStatus.Pending
        && careRequest.AssignedNurse.HasValue)
      .CountAsync(cancellationToken);

    var rejectedTodayCount = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest =>
        careRequest.Status == CareRequestStatus.Rejected
        && careRequest.RejectedAtUtc.HasValue
        && careRequest.RejectedAtUtc.Value >= utcToday
        && careRequest.RejectedAtUtc.Value < utcToday.AddDays(1))
      .CountAsync(cancellationToken);

    var approvedIncompleteCount = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest => careRequest.Status == CareRequestStatus.Approved)
      .CountAsync(cancellationToken);

    var overdueOrStaleCount = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest =>
        careRequest.Status != CareRequestStatus.Completed
        && (
          (careRequest.CareRequestDate.HasValue && careRequest.CareRequestDate.Value < currentCareDate)
          || (!careRequest.CareRequestDate.HasValue
              && careRequest.Status == CareRequestStatus.Pending
              && careRequest.UpdatedAtUtc <= staleCutoffUtc)
        ))
      .CountAsync(cancellationToken);

    var activeNursesCount = await _dbContext.Users
      .AsNoTracking()
      .Where(user =>
        user.ProfileType == UserProfileType.Nurse
        && user.IsActive
        && user.NurseProfile != null
        && user.NurseProfile.IsActive)
      .CountAsync(cancellationToken);

    var activeClientsCount = await _dbContext.Users
      .AsNoTracking()
      .Where(user =>
        user.ProfileType == UserProfileType.Client
        && user.IsActive
        && user.ClientProfile != null)
      .CountAsync(cancellationToken);

    return new AdminDashboardSnapshot(
      PendingNurseProfilesCount: pendingNurseProfilesCount,
      CareRequestsWaitingForAssignmentCount: waitingForAssignmentCount,
      CareRequestsWaitingForApprovalCount: waitingForApprovalCount,
      CareRequestsRejectedTodayCount: rejectedTodayCount,
      ApprovedCareRequestsStillIncompleteCount: approvedIncompleteCount,
      OverdueOrStaleRequestsCount: overdueOrStaleCount,
      ActiveNursesCount: activeNursesCount,
      ActiveClientsCount: activeClientsCount,
      UnreadAdminNotificationsCount: 0,
      HighSeverityAlerts: PlaceholderAlerts,
      GeneratedAtUtc: utcNow);
  }
}

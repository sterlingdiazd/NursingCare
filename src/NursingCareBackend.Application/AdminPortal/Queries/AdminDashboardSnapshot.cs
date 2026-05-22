namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminDashboardSnapshot(
  int PendingNurseProfilesCount,
  int CareRequestsWaitingForAssignmentCount,
  int CareRequestsWaitingForApprovalCount,
  int CareRequestsRejectedTodayCount,
  int ApprovedCareRequestsStillIncompleteCount,
  int OverdueOrStaleRequestsCount,
  int ActiveNursesCount,
  int ActiveClientsCount,
  int UnreadAdminNotificationsCount,
  int PendingDashboardTasksCount,
  int CompletedDashboardTasksTodayCount,
  int TotalDashboardTasksTodayCount,
  IReadOnlyList<AdminDashboardAlert> HighSeverityAlerts,
  DateTime GeneratedAtUtc);

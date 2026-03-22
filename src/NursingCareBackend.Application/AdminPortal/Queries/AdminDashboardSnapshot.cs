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
  IReadOnlyList<AdminDashboardAlert> HighSeverityAlerts,
  DateTime GeneratedAtUtc);

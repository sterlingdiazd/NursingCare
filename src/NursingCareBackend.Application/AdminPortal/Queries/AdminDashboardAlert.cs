namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminDashboardAlert(
  string Id,
  string Title,
  string Description,
  string Severity,
  string ModulePath);

namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminCareRequestListFilter(
  string? View,
  string? Search,
  DateOnly? ScheduledFrom,
  DateOnly? ScheduledTo,
  string? Sort);

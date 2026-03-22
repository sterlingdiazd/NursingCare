namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminCareRequestTimelineEvent(
  string Id,
  string Title,
  string Description,
  DateTime OccurredAtUtc);

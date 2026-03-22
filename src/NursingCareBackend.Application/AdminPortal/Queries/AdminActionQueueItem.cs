namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminActionQueueItem(
  string Id,
  string Severity,
  string State,
  string EntityType,
  string EntityIdentifier,
  string Summary,
  string RequiredAction,
  string? AssignedOwner,
  string DeepLinkPath,
  DateTime DetectedAtUtc);

namespace NursingCareBackend.Domain.Admin;

public sealed class AdminNotification
{
  public Guid Id { get; set; }
  public Guid RecipientUserId { get; set; }
  public string Category { get; set; } = string.Empty;
  public string Severity { get; set; } = "Medium";
  public string Title { get; set; } = string.Empty;
  public string Body { get; set; } = string.Empty;
  public string? EntityType { get; set; }
  public string? EntityId { get; set; }
  public string? DeepLinkPath { get; set; }
  public string? Source { get; set; }
  public bool RequiresAction { get; set; }
  public bool IsDismissed { get; set; }
  public DateTime CreatedAtUtc { get; set; }
  public DateTime? ReadAtUtc { get; set; }
  public DateTime? ArchivedAtUtc { get; set; }
  public bool CreatedBySystem { get; set; } = true;

  public bool IsRead => ReadAtUtc.HasValue;
  public bool IsArchived => ArchivedAtUtc.HasValue;
}

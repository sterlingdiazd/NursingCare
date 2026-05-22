namespace NursingCareBackend.Application.Notifications;

public sealed record ExpoPushMessage(
  string To,
  string Title,
  string Body,
  IReadOnlyDictionary<string, string?>? Data,
  string? Sound = "default",
  string? ChannelId = null,
  int? BadgeCount = null);

/// <summary>
/// Result of one push send attempt. Either ok with a ticket id, or an error
/// with a known status (DeviceNotRegistered, MessageRateExceeded, MessageTooBig,
/// InvalidCredentials, etc.).
/// </summary>
public sealed record ExpoPushTicket(
  string Token,
  string? TicketId,
  string Status,           // "ok" | "error"
  string? ErrorCode,       // populated when Status == "error"
  string? ErrorMessage);

public sealed record ExpoPushReceipt(
  string TicketId,
  string Status,           // "ok" | "error"
  string? ErrorCode,
  string? ErrorMessage);

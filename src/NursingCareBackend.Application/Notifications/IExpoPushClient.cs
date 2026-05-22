namespace NursingCareBackend.Application.Notifications;

/// <summary>
/// Thin client for the Expo Push Service. The dispatcher worker is the only
/// caller. Implementation is responsible for: chunking to 100 messages per
/// HTTP request (Expo's hard limit), retrying transient errors (5xx / 429),
/// and parsing per-token ticket / receipt status codes.
/// </summary>
public interface IExpoPushClient
{
  Task<IReadOnlyList<ExpoPushTicket>> SendBatchAsync(
    IReadOnlyCollection<ExpoPushMessage> messages,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<ExpoPushReceipt>> GetReceiptsAsync(
    IReadOnlyCollection<string> ticketIds,
    CancellationToken cancellationToken = default);
}

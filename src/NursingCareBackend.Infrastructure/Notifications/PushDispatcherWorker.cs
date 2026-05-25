using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.Admin;
using NursingCareBackend.Domain.Notifications;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Notifications;

/// <summary>
/// Drains the <c>NotificationOutbox</c>. Every cycle:
///   1. Claim up to <see cref="BatchSize"/> Pending rows by flipping them to Sending.
///   2. Join to the inbox row + each recipient's active push tokens.
///   3. Build Expo messages, send via <see cref="IExpoPushClient"/>.
///   4. Write back ticket ids on success, mark Sent. On per-token errors,
///      flag the corresponding <see cref="UserPushToken"/> inactive when the
///      Expo error is <c>DeviceNotRegistered</c>.
/// Receipt polling for pending tickets runs every <see cref="ReceiptPollInterval"/>.
/// </summary>
public sealed class PushDispatcherWorker : BackgroundService
{
  private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
  private static readonly TimeSpan ReceiptPollInterval = TimeSpan.FromMinutes(15);
  private const int BatchSize = 100;
  private const int MaxAttempts = 5;

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<PushDispatcherWorker> _logger;
  private DateTime _lastReceiptPollUtc = DateTime.MinValue;

  public PushDispatcherWorker(IServiceScopeFactory scopeFactory, ILogger<PushDispatcherWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("PushDispatcherWorker started");
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await DispatchOnceAsync(stoppingToken);
        if (DateTime.UtcNow - _lastReceiptPollUtc > ReceiptPollInterval)
        {
          await PollReceiptsAsync(stoppingToken);
          _lastReceiptPollUtc = DateTime.UtcNow;
        }
      }
      catch (OperationCanceledException) { }
      catch (Exception ex)
      {
        _logger.LogError(ex, "PushDispatcherWorker iteration failed");
      }
      await Task.Delay(PollInterval, stoppingToken);
    }
  }

  private async Task DispatchOnceAsync(CancellationToken cancellationToken)
  {
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var expo = scope.ServiceProvider.GetRequiredService<IExpoPushClient>();

    var pending = await db.NotificationOutbox
      .Where(o => o.Status == NotificationOutboxStatus.Pending && o.Attempts < MaxAttempts)
      .OrderBy(o => o.CreatedAtUtc)
      .Take(BatchSize)
      .ToListAsync(cancellationToken);

    if (pending.Count == 0) return;

    foreach (var row in pending)
    {
      row.Status = NotificationOutboxStatus.Sending;
      row.Attempts += 1;
      row.LastAttemptAtUtc = DateTime.UtcNow;
    }
    await db.SaveChangesAsync(cancellationToken);

    var adminNotificationIds = pending
      .Where(o => o.Kind == NotificationOutboxKind.Admin)
      .Select(o => o.NotificationId)
      .Distinct()
      .ToList();
    var userNotificationIds = pending
      .Where(o => o.Kind == NotificationOutboxKind.User)
      .Select(o => o.NotificationId)
      .Distinct()
      .ToList();

    var adminNotifications = await db.AdminNotifications
      .AsNoTracking()
      .Where(n => adminNotificationIds.Contains(n.Id))
      .Select(n => new PushNotificationEnvelope(
        n.Id,
        n.Category,
        n.Severity,
        n.Title,
        n.Body,
        n.DeepLinkPath,
        n.EntityType,
        n.EntityId,
        n.Source))
      .ToListAsync(cancellationToken);
    var userNotifications = await db.UserNotifications
      .AsNoTracking()
      .Where(n => userNotificationIds.Contains(n.Id))
      .Select(n => new PushNotificationEnvelope(
        n.Id,
        n.Category,
        n.Severity,
        n.Title,
        n.Body,
        n.DeepLinkPath,
        n.EntityType,
        n.EntityId,
        n.Source))
      .ToListAsync(cancellationToken);

    var notifications = adminNotifications
      .Select(n => (Kind: NotificationOutboxKind.Admin, Notification: n))
      .Concat(userNotifications.Select(n => (Kind: NotificationOutboxKind.User, Notification: n)))
      .ToDictionary(item => (item.Kind, item.Notification.Id), item => item.Notification);

    var recipientIds = pending.Select(o => o.RecipientUserId).Distinct().ToList();
    var tokensByUser = await db.UserPushTokens
      .Where(t => t.IsActive && recipientIds.Contains(t.UserId))
      .ToListAsync(cancellationToken);

    var tokensLookup = tokensByUser
      .GroupBy(t => t.UserId)
      .ToDictionary(g => g.Key, g => g.ToList());

    var messages = new List<(NotificationOutbox Row, UserPushToken Token, ExpoPushMessage Message)>();
    foreach (var row in pending)
    {
      if (!notifications.TryGetValue((row.Kind, row.NotificationId), out var notif))
      {
        row.Status = NotificationOutboxStatus.Failed;
        row.LastError = "NotificationMissing";
        continue;
      }
      if (!tokensLookup.TryGetValue(row.RecipientUserId, out var userTokens) || userTokens.Count == 0)
      {
        // No active tokens for this user — mark Sent (inbox row is already
        // persisted; nothing more to deliver).
        row.Status = NotificationOutboxStatus.Sent;
        continue;
      }
      foreach (var t in userTokens)
      {
        var data = new Dictionary<string, string?>
        {
          ["notificationId"] = notif.Id.ToString(),
          ["category"] = notif.Category,
          ["deepLinkPath"] = notif.DeepLinkPath,
          ["entityType"] = notif.EntityType,
          ["entityId"] = notif.EntityId,
          ["kind"] = row.Kind.ToString(),
          ["source"] = notif.Source,
        };
        messages.Add((row, t, new ExpoPushMessage(
          To: t.ExpoPushToken,
          Title: notif.Title,
          Body: notif.Body,
          Data: data,
          Sound: notif.Severity == "High" ? "default" : null)));
      }
    }

    if (messages.Count > 0)
    {
      var tickets = await expo.SendBatchAsync(messages.Select(m => m.Message).ToList(), cancellationToken);
      // Tickets come back in the same order as messages. Map by index.
      for (int i = 0; i < tickets.Count && i < messages.Count; i++)
      {
        var (row, token, _) = messages[i];
        var ticket = tickets[i];
        if (ticket.Status == "ok")
        {
          row.ExpoTicketId ??= ticket.TicketId;
          // Don't flip Sent yet — receipts arrive async. Mark Sent after first OK
          // ticket per row to keep state simple; receipt poll will downgrade to
          // Failed if Expo later reports a delivery error.
          row.Status = NotificationOutboxStatus.Sent;
        }
        else
        {
          row.LastError = ticket.ErrorCode ?? ticket.ErrorMessage ?? "Unknown";
          if (string.Equals(ticket.ErrorCode, "DeviceNotRegistered", StringComparison.Ordinal))
          {
            token.IsActive = false;
            token.LastFailureAtUtc = DateTime.UtcNow;
            token.FailureReason = ticket.ErrorCode;
            db.UserPushTokens.Update(token);
          }
          if (row.Attempts >= MaxAttempts)
          {
            row.Status = NotificationOutboxStatus.Failed;
          }
          else
          {
            row.Status = NotificationOutboxStatus.Pending;
          }
        }
      }
    }

    db.NotificationOutbox.UpdateRange(pending);
    await db.SaveChangesAsync(cancellationToken);
  }

  private async Task PollReceiptsAsync(CancellationToken cancellationToken)
  {
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var expo = scope.ServiceProvider.GetRequiredService<IExpoPushClient>();

    var pendingReceipts = await db.NotificationOutbox
      .Where(o => o.Status == NotificationOutboxStatus.Sent
               && o.ExpoTicketId != null
               && o.LastAttemptAtUtc != null
               && o.LastAttemptAtUtc < DateTime.UtcNow.AddMinutes(-5))
      .Take(BatchSize)
      .ToListAsync(cancellationToken);
    if (pendingReceipts.Count == 0) return;

    var ids = pendingReceipts
      .Select(o => o.ExpoTicketId!)
      .Distinct()
      .ToList();
    var receipts = await expo.GetReceiptsAsync(ids, cancellationToken);
    var byId = receipts.ToDictionary(r => r.TicketId);

    var tokensToDeactivate = new List<string>();
    foreach (var row in pendingReceipts)
    {
      if (row.ExpoTicketId is null) continue;
      if (!byId.TryGetValue(row.ExpoTicketId, out var receipt)) continue;
      if (receipt.Status == "ok") continue;

      row.Status = NotificationOutboxStatus.Failed;
      row.LastError = receipt.ErrorCode ?? receipt.ErrorMessage ?? "Unknown";
      if (string.Equals(receipt.ErrorCode, "DeviceNotRegistered", StringComparison.Ordinal))
      {
        // We don't know which token sent this ticket directly without joining;
        // but the token will hit the same error on the next attempt and be
        // flagged in the send loop. Leaving deactivation to that path keeps
        // this method simple.
      }
    }
    db.NotificationOutbox.UpdateRange(pendingReceipts);
    await db.SaveChangesAsync(cancellationToken);
  }

  private sealed record PushNotificationEnvelope(
    Guid Id,
    string Category,
    string Severity,
    string Title,
    string Body,
    string? DeepLinkPath,
    string? EntityType,
    string? EntityId,
    string? Source);
}

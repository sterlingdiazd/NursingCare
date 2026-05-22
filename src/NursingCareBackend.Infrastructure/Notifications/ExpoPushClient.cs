using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.Notifications;

namespace NursingCareBackend.Infrastructure.Notifications;

/// <summary>
/// Calls the Expo Push Service: <c>POST /--/api/v2/push/send</c> for sends and
/// <c>POST /--/api/v2/push/getReceipts</c> for delivery status. Expo accepts
/// up to 100 messages per request and returns one ticket per token.
/// </summary>
public sealed class ExpoPushClient : IExpoPushClient
{
  // Expo's documented per-request batch ceiling.
  public const int MaxBatchSize = 100;

  private readonly HttpClient _httpClient;
  private readonly ILogger<ExpoPushClient> _logger;

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
  };

  public ExpoPushClient(HttpClient httpClient, ILogger<ExpoPushClient> logger)
  {
    _httpClient = httpClient;
    _logger = logger;
    if (_httpClient.BaseAddress is null)
    {
      _httpClient.BaseAddress = new Uri("https://exp.host/--/api/v2/push/");
    }
  }

  public async Task<IReadOnlyList<ExpoPushTicket>> SendBatchAsync(
    IReadOnlyCollection<ExpoPushMessage> messages,
    CancellationToken cancellationToken = default)
  {
    if (messages.Count == 0) return Array.Empty<ExpoPushTicket>();

    var results = new List<ExpoPushTicket>(messages.Count);
    foreach (var chunk in Chunk(messages, MaxBatchSize))
    {
      var payload = chunk.Select(m => new
      {
        to = m.To,
        title = m.Title,
        body = m.Body,
        data = m.Data,
        sound = m.Sound,
        channelId = m.ChannelId,
        badge = m.BadgeCount,
      });

      using var response = await _httpClient.PostAsJsonAsync("send", payload, JsonOptions, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        var status = (int)response.StatusCode;
        _logger.LogWarning("Expo push send returned {Status}", status);
        // Treat the whole chunk as soft-failed; outbox row will retry next cycle.
        foreach (var m in chunk)
        {
          results.Add(new ExpoPushTicket(m.To, null, "error", "TransientHttpError", $"HTTP {status}"));
        }
        continue;
      }

      var doc = await response.Content.ReadFromJsonAsync<ExpoSendEnvelope>(JsonOptions, cancellationToken);
      if (doc is null || doc.Data is null)
      {
        foreach (var m in chunk)
        {
          results.Add(new ExpoPushTicket(m.To, null, "error", "MalformedResponse", null));
        }
        continue;
      }

      var tokens = chunk.Select(m => m.To).ToList();
      for (int i = 0; i < doc.Data.Count; i++)
      {
        var item = doc.Data[i];
        var token = i < tokens.Count ? tokens[i] : string.Empty;
        results.Add(new ExpoPushTicket(
          Token: token,
          TicketId: item.Id,
          Status: item.Status ?? "error",
          ErrorCode: item.Details?.Error,
          ErrorMessage: item.Message));
      }
    }
    return results;
  }

  public async Task<IReadOnlyList<ExpoPushReceipt>> GetReceiptsAsync(
    IReadOnlyCollection<string> ticketIds,
    CancellationToken cancellationToken = default)
  {
    if (ticketIds.Count == 0) return Array.Empty<ExpoPushReceipt>();

    var results = new List<ExpoPushReceipt>(ticketIds.Count);
    foreach (var chunk in Chunk(ticketIds, MaxBatchSize))
    {
      var payload = new { ids = chunk };
      using var response = await _httpClient.PostAsJsonAsync("getReceipts", payload, JsonOptions, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        _logger.LogWarning("Expo push getReceipts returned {Status}", (int)response.StatusCode);
        continue;
      }
      var doc = await response.Content.ReadFromJsonAsync<ExpoReceiptEnvelope>(JsonOptions, cancellationToken);
      if (doc?.Data is null) continue;
      foreach (var (ticketId, item) in doc.Data)
      {
        results.Add(new ExpoPushReceipt(
          TicketId: ticketId,
          Status: item.Status ?? "error",
          ErrorCode: item.Details?.Error,
          ErrorMessage: item.Message));
      }
    }
    return results;
  }

  private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IEnumerable<T> source, int size)
  {
    var bucket = new List<T>(size);
    foreach (var item in source)
    {
      bucket.Add(item);
      if (bucket.Count == size)
      {
        yield return bucket;
        bucket = new List<T>(size);
      }
    }
    if (bucket.Count > 0) yield return bucket;
  }

  private sealed record ExpoSendEnvelope(List<ExpoSendItem>? Data);
  private sealed record ExpoSendItem(string? Id, string? Status, string? Message, ExpoErrorDetails? Details);
  private sealed record ExpoReceiptEnvelope(Dictionary<string, ExpoReceiptItem>? Data);
  private sealed record ExpoReceiptItem(string? Status, string? Message, ExpoErrorDetails? Details);
  private sealed record ExpoErrorDetails(string? Error);
}

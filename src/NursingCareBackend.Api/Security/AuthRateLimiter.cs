namespace NursingCareBackend.Api.Security;

public interface IAuthRateLimiter
{
    AuthRateLimitDecision Check(string bucket, string clientIpAddress, int limit, TimeSpan window);
}

public sealed record AuthRateLimitDecision(bool IsAllowed, TimeSpan RetryAfter);

public sealed class AuthRateLimiter : IAuthRateLimiter
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthRateLimiter> _logger;
    private readonly object _syncLock = new();
    private readonly Dictionary<string, AuthRateLimitCounter> _counters = new(StringComparer.Ordinal);

    public AuthRateLimiter(
        TimeProvider timeProvider,
        ILogger<AuthRateLimiter> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public AuthRateLimitDecision Check(string bucket, string clientIpAddress, int limit, TimeSpan window)
    {
        var now = _timeProvider.GetUtcNow();
        var cacheKey = $"auth-rate:{bucket}:{clientIpAddress}";

        lock (_syncLock)
        {
            PruneExpiredCounters(now);

            if (!_counters.TryGetValue(cacheKey, out var counter)
                || counter.ExpiresAtUtc <= now)
            {
                _counters[cacheKey] = new AuthRateLimitCounter(
                    Count: 1,
                    WindowStartedAtUtc: now,
                    ExpiresAtUtc: now.Add(window));
                return new AuthRateLimitDecision(true, TimeSpan.Zero);
            }

            if (counter.Count >= limit)
            {
                var retryAfter = counter.ExpiresAtUtc - now;
                _logger.LogWarning(
                    "Auth IP rate limit exceeded. Bucket={Bucket} ClientIpAddress={ClientIpAddress} RetryAfterSeconds={RetryAfterSeconds}",
                    bucket,
                    clientIpAddress,
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)));
                return new AuthRateLimitDecision(false, retryAfter);
            }

            _counters[cacheKey] = counter with { Count = counter.Count + 1 };
            return new AuthRateLimitDecision(true, TimeSpan.Zero);
        }
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _counters.Clear();
        }
    }

    private void PruneExpiredCounters(DateTimeOffset now)
    {
        if (_counters.Count == 0)
        {
            return;
        }

        var expiredKeys = _counters
            .Where(entry => entry.Value.ExpiresAtUtc <= now)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var expiredKey in expiredKeys)
        {
            _counters.Remove(expiredKey);
        }
    }

    private sealed record AuthRateLimitCounter(
        int Count,
        DateTimeOffset WindowStartedAtUtc,
        DateTimeOffset ExpiresAtUtc);
}

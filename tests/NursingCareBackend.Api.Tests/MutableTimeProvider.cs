namespace NursingCareBackend.Api.Tests;

public sealed class MutableTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset DefaultUtcNow = new(2026, 4, 2, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _utcNow = DefaultUtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);

    public void Reset() => _utcNow = DefaultUtcNow;
}

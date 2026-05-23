namespace NursingCareBackend.Api.Tests;

public sealed class MutableTimeProvider : TimeProvider
{
    private static DateTimeOffset DefaultUtcNow => DateTimeOffset.UtcNow;

    private DateTimeOffset _utcNow = DefaultUtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);

    public void Reset() => _utcNow = DefaultUtcNow;
}

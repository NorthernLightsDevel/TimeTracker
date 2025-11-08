using System;

namespace TimeTracker.Application.Tests.Infrastructure;

/// <summary>
/// Provides a mutable <see cref="TimeProvider"/> for unit tests so scenarios can advance time deterministically.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset initialUtcNow)
    {
        _utcNow = EnsureUtc(initialUtcNow);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void Advance(TimeSpan delta)
    {
        if (delta == TimeSpan.Zero)
        {
            return;
        }

        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Advance delta must be non-negative.");
        }

        _utcNow = _utcNow.Add(delta);
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        _utcNow = EnsureUtc(value);
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime();
    }
}

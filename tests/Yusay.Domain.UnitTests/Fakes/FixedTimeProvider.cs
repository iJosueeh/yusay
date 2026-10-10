namespace Yusay.Domain.UnitTests.Fakes;

public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public DateTimeOffset UtcNow
    {
        get => _utcNow;
        set => _utcNow = value;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

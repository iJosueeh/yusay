namespace Yusay.Domain.UnitTests.Fakes;

/// <summary>
/// Reloj controlable para pruebas de vigencia (exp), de emisión (iat) y de la ventana de un
/// segundo que exige la revocación MP-PHYS-015.
/// </summary>
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

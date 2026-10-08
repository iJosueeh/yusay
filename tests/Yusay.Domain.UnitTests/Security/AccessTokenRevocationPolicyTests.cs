using Yusay.Application.Identity.Tokens;

namespace Yusay.Domain.UnitTests.Security;

/// <summary>
/// Política de revocación MP-PHYS-015: la regla aprobada en segundos
/// (<c>iat &gt;= floor(epoch(password_changed_at))</c>) acumulada a la huella exacta de la versión de
/// credencial (<c>pwd_at</c>, microsegundos), que resuelve el segundo ambiguo sin perder precisión.
/// </summary>
public class AccessTokenRevocationPolicyTests
{
    private static readonly DateTimeOffset BaseInstant =
        new DateTimeOffset(2026, 10, 8, 12, 30, 45, TimeSpan.Zero);

    /// <summary>Cambio con fracción de segundo: 45.1234567 s → 123456 µs (timestamptz trunca a µs).</summary>
    private static readonly DateTimeOffset PasswordChangedAt = BaseInstant.AddTicks(1_234_567);

    private static long Version(DateTimeOffset passwordChangedAt) =>
        AccessTokenRevocationPolicy.ToCredentialVersion(passwordChangedAt);

    [Fact]
    public void ToCredentialVersion_ShouldExpressPasswordChangedAtInWholeMicroseconds()
    {
        // 12:30:45.1234567 s → el microsegundo almacenado es .123456: 45 s y 123456 µs desde la época.
        Assert.Equal(
            PasswordChangedAt.ToUnixTimeSeconds() * 1_000_000 + 123_456,
            AccessTokenRevocationPolicy.ToCredentialVersion(PasswordChangedAt));
    }

    [Fact]
    public void ToCredentialVersion_ShouldDistinguishChangesWithinTheSameSecond()
    {
        var firstChange = PasswordChangedAt;
        var secondChange = firstChange.AddMicroseconds(1);

        Assert.NotEqual(Version(firstChange), Version(secondChange));
    }

    [Fact]
    public void IsApprovedIatRuleSatisfied_ShouldMatchTheDocumentedLiteralRule()
    {
        var floor = PasswordChangedAt.ToUnixTimeSeconds();

        Assert.True(AccessTokenRevocationPolicy.IsApprovedIatRuleSatisfied(floor, PasswordChangedAt));
        Assert.False(AccessTokenRevocationPolicy.IsApprovedIatRuleSatisfied(floor - 1, PasswordChangedAt));
    }

    [Fact]
    public void IsAccepted_TokenIssuedBeforeTheChange_ShouldBeRejected()
    {
        var issuedBefore = PasswordChangedAt.AddMinutes(-5).ToUnixTimeSeconds();

        Assert.False(AccessTokenRevocationPolicy.IsAccepted(issuedBefore, Version(PasswordChangedAt), PasswordChangedAt));
    }

    [Fact]
    public void IsAccepted_TokenInTheSameSecondCarryingThePreviousVersion_ShouldBeRejected()
    {
        // Token emitido microsegundos ANTES del cambio: mismo iat que el segundo ambiguo, huella
        // anterior. Sin la huella sería indistinguible de uno emitido después.
        var previousVersion = Version(PasswordChangedAt.AddSeconds(-3));
        var sameSecond = PasswordChangedAt.ToUnixTimeSeconds();

        Assert.False(AccessTokenRevocationPolicy.IsAccepted(sameSecond, previousVersion, PasswordChangedAt));
    }

    [Fact]
    public void IsAccepted_TokenInTheSameSecondIssuedAfterTheChange_ShouldBeAccepted()
    {
        // Token emitido microsegundos DESPUÉS del cambio dentro del mismo segundo: la regla en
        // segundos lo admite y la huella confirma que pertenece a la versión vigente.
        var sameSecond = PasswordChangedAt.ToUnixTimeSeconds();

        Assert.True(AccessTokenRevocationPolicy.IsAccepted(sameSecond, Version(PasswordChangedAt), PasswordChangedAt));
    }

    [Fact]
    public void IsAccepted_TokenIssuedOneSecondAfterTheChange_ShouldBeAccepted()
    {
        var issuedNextSecond = PasswordChangedAt.ToUnixTimeSeconds() + 1;

        Assert.True(AccessTokenRevocationPolicy.IsAccepted(issuedNextSecond, Version(PasswordChangedAt), PasswordChangedAt));
    }

    [Fact]
    public void IsAccepted_TokenIssuedLongAfterTheChange_ShouldBeAccepted()
    {
        var issuedAfter = PasswordChangedAt.AddHours(3).ToUnixTimeSeconds();

        Assert.True(AccessTokenRevocationPolicy.IsAccepted(issuedAfter, Version(PasswordChangedAt), PasswordChangedAt));
    }

    [Fact]
    public void IsAccepted_LaterTokenCarryingAStaleCredentialVersion_ShouldBeRejected()
    {
        // iat posterior al cambio pero anclado a una versión antigua: la huella veta el token aunque
        // la regla en segundos lo admitiría.
        var staleVersion = Version(PasswordChangedAt.AddHours(-3));
        var issuedAfter = PasswordChangedAt.AddMinutes(5).ToUnixTimeSeconds();

        Assert.False(AccessTokenRevocationPolicy.IsAccepted(issuedAfter, staleVersion, PasswordChangedAt));
    }

    [Fact]
    public void IsAccepted_WhenPasswordChangedAtFallsExactlyOnASecondBoundary_ShouldDistinguishByVersion()
    {
        var boundary = BaseInstant;
        var sameSecond = boundary.ToUnixTimeSeconds();

        // Mismo iat exacto: lo único que separa al token previo del vigente es la huella.
        Assert.False(AccessTokenRevocationPolicy.IsAccepted(sameSecond, Version(boundary.AddSeconds(-1)), boundary));
        Assert.True(AccessTokenRevocationPolicy.IsAccepted(sameSecond, Version(boundary), boundary));
    }

    [Fact]
    public void IsAccepted_AfterTwoChangesInTheSameSecond_ShouldRejectTheIntermediateToken()
    {
        // Dos cambios en el mismo segundo (el segundo avanza 1 µs por monotonicidad): el token
        // emitido entre ambos deja de valer para el segundo cambio.
        var firstChange = PasswordChangedAt;
        var secondChange = firstChange.AddMicroseconds(1);
        var sameSecond = secondChange.ToUnixTimeSeconds();

        Assert.False(AccessTokenRevocationPolicy.IsAccepted(sameSecond, Version(firstChange), secondChange));
        Assert.True(AccessTokenRevocationPolicy.IsAccepted(sameSecond, Version(secondChange), secondChange));
    }

    [Fact]
    public void IsAccepted_ShouldNeverAdmitATokenTheApprovedRuleRejects()
    {
        // Propiedad de compatibilidad: el conjunto aceptado es subconjunto del que admite la regla
        // documentada; la política solo rechaza más, nunca menos.
        var baseInstant = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        for (var secondsAfterBase = 0; secondsAfterBase < 60; secondsAfterBase += 7)
        {
            var changeInstant = baseInstant.AddSeconds(secondsAfterBase).AddTicks(secondsAfterBase * 13);
            var currentVersion = Version(changeInstant);
            var staleVersion = Version(changeInstant.AddSeconds(-90));

            for (var issuedAt = baseInstant.ToUnixTimeSeconds() - 10;
                 issuedAt < changeInstant.ToUnixTimeSeconds() + 3;
                 issuedAt++)
            {
                foreach (var version in new[] { currentVersion, staleVersion })
                {
                    var accepted = AccessTokenRevocationPolicy.IsAccepted(issuedAt, version, changeInstant);
                    var approvedRuleAdmits = AccessTokenRevocationPolicy.IsApprovedIatRuleSatisfied(issuedAt, changeInstant);

                    if (accepted)
                    {
                        Assert.True(approvedRuleAdmits, $"iat={issuedAt} fue aceptado pese a violar la regla aprobada.");
                    }

                    if (issuedAt < changeInstant.ToUnixTimeSeconds())
                    {
                        Assert.False(accepted, $"iat={issuedAt} anterior a {changeInstant:O} debió rechazarse.");
                    }

                    if (version != currentVersion)
                    {
                        Assert.False(accepted, $"iat={issuedAt} con huella obsoleta debió rechazarse.");
                    }
                }
            }
        }
    }

    [Fact]
    public void IsAccepted_AcceptsEveryPostChangeTokenCarryingTheVigentVersion()
    {
        // Requisito de aceptación inmediata: todo token emitido después del cambio con la huella
        // vigente pasa, incluido el emitido en su mismo segundo.
        var baseInstant = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        for (var secondsAfterBase = 0; secondsAfterBase < 60; secondsAfterBase += 7)
        {
            var changeInstant = baseInstant.AddSeconds(secondsAfterBase).AddTicks(secondsAfterBase * 13);
            var currentVersion = Version(changeInstant);

            for (var issuedAt = changeInstant.ToUnixTimeSeconds();
                 issuedAt < changeInstant.ToUnixTimeSeconds() + 3;
                 issuedAt++)
            {
                Assert.True(
                    AccessTokenRevocationPolicy.IsAccepted(issuedAt, currentVersion, changeInstant),
                    $"iat={issuedAt} emitido tras {changeInstant:O} con huella vigente debió aceptarse.");
            }
        }
    }
}

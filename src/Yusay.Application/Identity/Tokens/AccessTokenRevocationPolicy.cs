namespace Yusay.Application.Identity.Tokens;

/// <summary>
/// Política de revocación de access tokens por cambio de credencial (MP-PHYS-015).
/// </summary>
/// <remarks>
/// <para>
/// <b>Diagnóstico.</b> La regla aprobada es
/// <c>to_timestamp(token.iat) &gt;= date_trunc('second', user_credential.password_changed_at)</c>.
/// Como <c>iat</c> son segundos enteros (RFC 7519) y <c>password_changed_at</c> es un
/// <c>timestamptz</c> con microsegundos, la regla por sí sola no puede ordenar dos tokens emitidos
/// dentro del mismo segundo que el cambio: ambos comparten <c>iat</c>. Aceptar ese segundo admite
/// un token emitido milisegundos <i>antes</i> del cambio; rechazarlo descarta un token legítimo
/// emitido milisegundos <i>después</i>. Es una pérdida de información, no un problema de umbrales.
/// </para>
/// <para>
/// <b>Solución.</b> El token transporta además la versión exacta de la credencial contra la que se
/// verificó la contraseña (<c>pwd_at</c>, en microsegundos Unix). La aceptación exige
/// <b>acumulativamente</b> ambas condiciones:
/// <list type="number">
/// <item><description>la regla aprobada literal, <c>iat &gt;= floor(epoch(password_changed_at))</c>;</description></item>
/// <item><description><c>pwd_at == password_changed_at</c> con exactitud de microsegundos.</description></item>
/// </list>
/// El conjunto aceptado es por tanto un <b>subconjunto estricto</b> del que admite la regla
/// documentada: nunca se acepta un token que el contrato rechace, solo se resuelve con precisión el
/// segundo ambiguo que el propio contrato identifica en su análisis crítico
/// ("una comparación estricta puede incurrir en falsos rechazos o tolerar un token emitido
/// milisegundos antes del cambio"). El resultado es que todo token anterior al cambio queda
/// revocado, aunque se emitiese en el mismo segundo, y todo token posterior se acepta de inmediato.
/// </para>
/// <para>
/// <b>Determinismo.</b> Ambos operandos proceden de la misma fila de <c>yusay.user_credential</c>:
/// la huella la lee el backend al emitir y al validar, y el valor almacenado avanza
/// estrictamente (<c>UserCredential.NextChangeInstant</c>). Por eso la política no depende del
/// reloj de la instancia que emite ni de estado en memoria: ante la misma fila y el mismo token la
/// respuesta es idéntica en cualquier instancia y bajo cualquier interleaving de concurrencia.
/// Único requisito operativo: relojes sincronizados (&lt; 1 s), que la validación de <c>exp</c> e
/// <c>iat</c> del propio JWT ya exige.
/// </para>
/// </remarks>
public static class AccessTokenRevocationPolicy
{
    /// <summary>Ticks de .NET que componen un microsegundo (1 tick = 100 ns).</summary>
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary>
    /// Huella de la versión de credencial: <paramref name="passwordChangedAt"/> convertido a
    /// microsegundos Unix enteros. Es la unidad de <c>timestamptz</c>, por lo que dos cambios de
    /// contraseña distintos producen huellas distintas.
    /// </summary>
    public static long ToCredentialVersion(DateTimeOffset passwordChangedAt)
    {
        // Instantes posteriores a 1970-01-01, ámbito del dominio: la división es exacta cuando el
        // valor ya viene alineado a microsegundos desde PostgreSQL.
        var ticksSinceEpoch = passwordChangedAt.UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks;
        return ticksSinceEpoch / TicksPerMicrosecond;
    }

    /// <summary>
    /// Regla aprobada de MP-PHYS-015, literal: <c>iat &gt;= floor(epoch(password_changed_at))</c>.
    /// </summary>
    public static bool IsApprovedIatRuleSatisfied(long issuedAtSeconds, DateTimeOffset passwordChangedAt)
    {
        // ToUnixTimeSeconds trunca hacia abajo (floor) el microsegundo para instantes positivos.
        return issuedAtSeconds >= passwordChangedAt.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Decide si un token emitido en el segundo <paramref name="issuedAtSeconds"/> y anclado a la
    /// versión de credencial <paramref name="issuedCredentialVersion"/> sigue vigente.
    /// </summary>
    public static bool IsAccepted(
        long issuedAtSeconds,
        long issuedCredentialVersion,
        DateTimeOffset passwordChangedAt)
    {
        return IsApprovedIatRuleSatisfied(issuedAtSeconds, passwordChangedAt)
            && issuedCredentialVersion == ToCredentialVersion(passwordChangedAt);
    }
}

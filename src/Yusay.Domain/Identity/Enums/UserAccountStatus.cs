namespace Yusay.Domain.Identity.Enums;

/// <summary>
/// Estado operativo de la cuenta de usuario (R-001 / ck_user_account_status).
/// </summary>
public enum UserAccountStatus
{
    /// <summary>
    /// Cuenta activa y operativa.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Cuenta bloqueada por motivos de seguridad o administrativos.
    /// </summary>
    Blocked = 2
}

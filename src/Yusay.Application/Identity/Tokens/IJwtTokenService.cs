namespace Yusay.Application.Identity.Tokens;

public interface IJwtTokenService
{
    /// <summary>
    /// Emite un access token anclado a la versión de credencial <paramref name="passwordChangedAt"/>
    /// (claim <c>pwd_at</c>), que es la que el backend debe seguir vigente en cada validación
    /// (MP-PHYS-015).
    /// </summary>
    IssuedAccessToken IssueAccessToken(Guid userId, string email, DateTimeOffset passwordChangedAt);

    AccessTokenValidationResult ValidateAccessToken(string accessToken);
}

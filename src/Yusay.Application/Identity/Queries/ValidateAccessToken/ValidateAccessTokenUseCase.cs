using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Repositories;
using Yusay.Application.Identity.Tokens;

namespace Yusay.Application.Identity.Queries.ValidateAccessToken;

public sealed class ValidateAccessTokenUseCase(
    IUserAccountRepository userAccountRepository,
    IUserCredentialRepository userCredentialRepository,
    IJwtTokenService jwtTokenService,
    IAccessTokenDenylist accessTokenDenylist) : IValidateAccessTokenUseCase
{
    private const string TokenRejectedMessage = "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IUserCredentialRepository _userCredentialRepository = userCredentialRepository;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IAccessTokenDenylist _accessTokenDenylist = accessTokenDenylist;

    public async Task<ValidateAccessTokenResult> ExecuteAsync(ValidateAccessTokenQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.AccessToken))
        {
            throw new ValidationException("El token de acceso es obligatorio.");
        }

        var validation = _jwtTokenService.ValidateAccessToken(query.AccessToken);
        if (!validation.IsValid)
        {
            throw new UnauthorizedException(TokenRejectedMessage);
        }
        
        if (await _accessTokenDenylist.IsRevokedAsync(validation.TokenId, cancellationToken))
        {
            throw new UnauthorizedException(TokenRejectedMessage);
        }

        var user = await _userAccountRepository.GetByIdAsync(validation.UserId, transaction: null, cancellationToken);
        if (user is null || !user.CanAccessPersonalFeatures)
        {
            throw new UnauthorizedException(TokenRejectedMessage);
        }

        var credential = await _userCredentialRepository.GetByUserIdAsync(user.Id, transaction: null, cancellationToken);
        if (credential is null)
        {
            throw new UnauthorizedException(TokenRejectedMessage);
        }

        if (!AccessTokenRevocationPolicy.IsAccepted(
                validation.IssuedAtSeconds,
                validation.PasswordChangedAtMicroseconds,
                credential.PasswordChangedAt))
        {
            throw new UnauthorizedException(TokenRejectedMessage);
        }

        return new ValidateAccessTokenResult(
            user.Id,
            user.Email.Value,
            validation.IssuedAt,
            validation.ExpiresAt);
    }
}

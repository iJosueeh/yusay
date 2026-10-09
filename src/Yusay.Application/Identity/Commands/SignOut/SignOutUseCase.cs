using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Identity.Commands.SignOut;

public sealed class SignOutUseCase(
    IUnitOfWork unitOfWork,
    IUserAccountRepository userAccountRepository,
    IUserCredentialRepository userCredentialRepository,
    IAuditEventRepository auditEventRepository,
    IJwtTokenService jwtTokenService,
    IAccessTokenDenylist accessTokenDenylist) : ISignOutUseCase
{
    private const string TokenRejectedMessage = "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IUserCredentialRepository _userCredentialRepository = userCredentialRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IAccessTokenDenylist _accessTokenDenylist = accessTokenDenylist;

    public async Task<SignOutResult> ExecuteAsync(SignOutCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.AccessToken))
        {
            throw new ValidationException("El token de acceso es obligatorio.");
        }

        var validation = _jwtTokenService.ValidateAccessToken(command.AccessToken);

        if (!validation.IsValid)
        {
            if (validation.Reason == AccessTokenRejectionReason.Expired)
            {
                return new SignOutResult(RevokedNow: false);
            }

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
            return new SignOutResult(RevokedNow: false);
        }

        var revokedNow = await _accessTokenDenylist.RevokeAsync(
            validation.TokenId,
            validation.ExpiresAt,
            cancellationToken);

        if (revokedNow)
        {
            await AuditSignOutPreservingRevocationAsync(user.Id, cancellationToken);
        }

        return new SignOutResult(revokedNow);
    }

    private async Task AuditSignOutPreservingRevocationAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await AuditSignOutAsync(userId, cancellationToken);
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                throw;
            }

            throw new ServiceUnavailableException(
                "No se pudo registrar la auditoría del cierre de sesión; la revocación permanece efectiva y el evento SIGN_OUT no se ha registrado.",
                exception);
        }
    }

    private async Task AuditSignOutAsync(Guid userId, CancellationToken cancellationToken)
    {
        var audit = AuditEvent.CreateSignOut(userId);

        await _unitOfWork.ExecuteInTransactionAsync(async transaction =>
        {
            await _auditEventRepository.AddAsync(audit, transaction, cancellationToken);
        }, cancellationToken);
    }
}

using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Application.Identity.Commands.SignIn;

public sealed class SignInUseCase(
    IUnitOfWork unitOfWork,
    IUserAccountRepository userAccountRepository,
    IUserCredentialRepository userCredentialRepository,
    IAuditEventRepository auditEventRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : ISignInUseCase
{
    private const string CredentialsRejectedMessage = "El correo electrónico o la contraseña no son válidos.";
    private const string AccountBlockedMessage = "La cuenta está bloqueada y no puede iniciar sesión.";
    private const string EmailNotVerifiedMessage = "El correo electrónico debe verificarse antes de iniciar sesión.";
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IUserCredentialRepository _userCredentialRepository = userCredentialRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

    private string? _discardablePasswordHash;

    private string DiscardablePasswordHash =>
        _discardablePasswordHash ??= _passwordHasher.HashPassword(Guid.NewGuid().ToString("N"));

    public async Task<SignInResult> ExecuteAsync(SignInCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            throw new ValidationException("El correo electrónico es obligatorio para iniciar sesión.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            throw new ValidationException("La contraseña es obligatoria para iniciar sesión.");
        }

        var email = Email.Create(command.Email);
        var now = DateTimeOffset.UtcNow;

        var user = await _userAccountRepository.GetByEmailAsync(email, transaction: null, cancellationToken);
        if (user is null)
        {
            _passwordHasher.VerifyPassword(command.Password, DiscardablePasswordHash);
            await AuditSignInFailedAsync(now, cancellationToken);
            throw new UnauthorizedException(CredentialsRejectedMessage);
        }

        var credential = await _userCredentialRepository.GetByUserIdAsync(user.Id, transaction: null, cancellationToken);
        if (credential is null || !_passwordHasher.VerifyPassword(command.Password, credential.PasswordHash))
        {
            await AuditSignInFailedAsync(now, cancellationToken);
            throw new UnauthorizedException(CredentialsRejectedMessage);
        }

        if (user.Status != UserAccountStatus.Active)
        {
            await AuditSignInFailedAsync(now, cancellationToken);
            throw new UnauthorizedException(AccountBlockedMessage);
        }

        if (!user.IsEmailVerified)
        {
            await AuditSignInFailedAsync(now, cancellationToken);
            throw new UnauthorizedException(EmailNotVerifiedMessage);
        }

        // La versión de credencial que se fija en el token es la leída junto a la contraseña que se
        // acaba de verificar: si la credencial cambia en este intervalo, el token nace revocado.
        var issuedToken = _jwtTokenService.IssueAccessToken(user.Id, user.Email.Value, credential.PasswordChangedAt);

        await AuditSignInSucceededAsync(user.Id, now, cancellationToken);

        return new SignInResult(
            user.Id,
            user.Email.Value,
            issuedToken.Token,
            TokenType: "Bearer",
            issuedToken.IssuedAt,
            issuedToken.ExpiresAt);
    }

    private async Task AuditSignInFailedAsync(DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var audit = AuditEvent.CreateSignInFailed(occurredAt);

        await _unitOfWork.ExecuteInTransactionAsync(async transaction =>
        {
            await _auditEventRepository.AddAsync(audit, transaction, cancellationToken);
        }, cancellationToken);
    }

    private async Task AuditSignInSucceededAsync(Guid userId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var audit = AuditEvent.CreateSignInSucceeded(userId, occurredAt);

        await _unitOfWork.ExecuteInTransactionAsync(async transaction =>
        {
            await _auditEventRepository.AddAsync(audit, transaction, cancellationToken);
        }, cancellationToken);
    }
}

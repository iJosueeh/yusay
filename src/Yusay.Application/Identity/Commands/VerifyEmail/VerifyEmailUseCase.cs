using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Identity.Commands.VerifyEmail;

public sealed class VerifyEmailUseCase(
    IUnitOfWork unitOfWork,
    IUserAccountRepository userAccountRepository,
    IEmailVerificationTokenRepository tokenRepository,
    IAuditEventRepository auditEventRepository,
    ISecureTokenService tokenService) : IVerifyEmailUseCase
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IEmailVerificationTokenRepository _tokenRepository = tokenRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;
    private readonly ISecureTokenService _tokenService = tokenService;

    public async Task<VerifyEmailResult> ExecuteAsync(VerifyEmailCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            throw new ValidationException("El token de verificación es obligatorio.");
        }

        var tokenHash = _tokenService.HashToken(command.Token);

        return await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var token = await _tokenRepository.GetByHashAsync(tokenHash, tx, forUpdate: true, cancellationToken);
            if (token is null)
            {
                throw new InvalidTokenException("El token de verificación no existe o ya ha sido consumido.");
            }

            var now = DateTimeOffset.UtcNow;
            if (token.IsExpired(now))
            {
                throw new InvalidTokenException("El token de verificación ha expirado.");
            }

            var user = await _userAccountRepository.GetByIdAsync(token.UserId, tx, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException("No se encontró la cuenta de usuario asociada al token de verificación.");
            }

            user.VerifyEmail(now);
            await _userAccountRepository.UpdateAsync(user, tx, cancellationToken);

            await _tokenRepository.InvalidateAllForUserAsync(user.Id, tx, cancellationToken);

            var audit = AuditEvent.CreateEmailVerified(user.Id, now);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            return new VerifyEmailResult(user.Id, user.Email.Value, now);
        }, cancellationToken);
    }
}

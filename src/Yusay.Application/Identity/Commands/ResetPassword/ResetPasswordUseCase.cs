using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Identity.Commands.ResetPassword;

public sealed class ResetPasswordUseCase(
    IUnitOfWork unitOfWork,
    IUserAccountRepository userAccountRepository,
    IUserCredentialRepository userCredentialRepository,
    IPasswordResetTokenRepository resetTokenRepository,
    IAuditEventRepository auditEventRepository,
    IPasswordHasher passwordHasher,
    ISecureTokenService tokenService) : IResetPasswordUseCase
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IUserCredentialRepository _userCredentialRepository = userCredentialRepository;
    private readonly IPasswordResetTokenRepository _resetTokenRepository = resetTokenRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ISecureTokenService _tokenService = tokenService;

    public async Task<ResetPasswordResult> ExecuteAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            throw new ValidationException("El token de recuperación es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword) || command.NewPassword.Length < 8)
        {
            throw new ValidationException("La nueva contraseña debe tener al menos 8 caracteres.");
        }

        var newPasswordHash = _passwordHasher.HashPassword(command.NewPassword);
        var tokenHash = _tokenService.HashToken(command.Token);

        return await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var token = await _resetTokenRepository.GetByHashAsync(tokenHash, tx, forUpdate: true, cancellationToken);
            if (token is null)
            {
                throw new InvalidTokenException("El token de recuperación no existe o ya ha sido consumido.");
            }

            var now = DateTimeOffset.UtcNow;
            if (token.IsExpired(now))
            {
                throw new InvalidTokenException("El token de recuperación ha expirado.");
            }

            var user = await _userAccountRepository.GetByIdAsync(token.UserId, tx, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException("No se encontró la cuenta de usuario asociada al token.");
            }

            var credential = await _userCredentialRepository.GetByUserIdAsync(user.Id, tx, cancellationToken);
            if (credential is null)
            {
                throw new NotFoundException("No se encontraron las credenciales del usuario.");
            }

            credential.ChangePassword(newPasswordHash, now);
            await _userCredentialRepository.UpdateAsync(credential, tx, cancellationToken);

            await _resetTokenRepository.InvalidateAllForUserAsync(user.Id, tx, cancellationToken);

            var audit = AuditEvent.CreatePasswordResetCompleted(user.Id, now);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            return new ResetPasswordResult(user.Id, user.Email.Value, now);
        }, cancellationToken);
    }
}

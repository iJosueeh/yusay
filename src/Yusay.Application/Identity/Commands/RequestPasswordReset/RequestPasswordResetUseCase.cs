using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Application.Identity.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetUseCase(
    IUnitOfWork unitOfWork,
    IUserAccountRepository userAccountRepository,
    IPasswordResetTokenRepository resetTokenRepository,
    IAuditEventRepository auditEventRepository,
    ISecureTokenService tokenService) : IRequestPasswordResetUseCase
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IPasswordResetTokenRepository _resetTokenRepository = resetTokenRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;
    private readonly ISecureTokenService _tokenService = tokenService;

    public async Task<RequestPasswordResetResult> ExecuteAsync(RequestPasswordResetCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            throw new ValidationException("El correo electrónico es obligatorio para solicitar la recuperación de contraseña.");
        }

        var email = Email.Create(command.Email);

        return await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var user = await _userAccountRepository.GetByEmailAsync(email, tx, cancellationToken);
            if (user is null)
            {
                // Regla de no revelación: respuesta indistinguible para correos no registrados
                return new RequestPasswordResetResult(EmailSent: true, ResetToken: null);
            }

            // Invalida cualquier token previo de recuperación para el usuario
            await _resetTokenRepository.InvalidateAllForUserAsync(user.Id, tx, cancellationToken);

            var rawToken = _tokenService.GenerateToken();
            var tokenHash = _tokenService.HashToken(rawToken);

            var now = DateTimeOffset.UtcNow;
            var resetToken = PasswordResetToken.Create(user.Id, tokenHash, now);

            await _resetTokenRepository.AddAsync(resetToken, tx, cancellationToken);

            var audit = AuditEvent.CreatePasswordResetTokenIssued(user.Id, now);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            return new RequestPasswordResetResult(EmailSent: true, ResetToken: rawToken);
        }, cancellationToken);
    }
}

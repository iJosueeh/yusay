using Microsoft.Extensions.Logging;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Identity.Commands.DeleteUserAccount;

public sealed class DeleteUserAccountUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IUserAccountRepository userAccountRepository,
    IUserCredentialRepository userCredentialRepository,
    IAuditEventRepository auditEventRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IAccessTokenDenylist accessTokenDenylist,
    TimeProvider? timeProvider = null,
    ILogger<DeleteUserAccountUseCase>? logger = null) : IDeleteUserAccountUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IUserCredentialRepository _userCredentialRepository = userCredentialRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IAccessTokenDenylist _accessTokenDenylist = accessTokenDenylist;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ILogger<DeleteUserAccountUseCase>? _logger = logger;

    public async Task ExecuteAsync(DeleteUserAccountCommand command, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para suprimir la cuenta.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            throw new ValidationException("La contraseña actual es obligatoria para suprimir la cuenta.");
        }

        var user = await _userAccountRepository.GetByIdAsync(userId.Value, transaction: null, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedException("Usuario no encontrado.");
        }

        var credential = await _userCredentialRepository.GetByUserIdAsync(user.Id, transaction: null, cancellationToken);
        if (credential is null || !_passwordHasher.VerifyPassword(command.Password, credential.PasswordHash))
        {
            throw new UnauthorizedException("Credenciales no válidas.");
        }

        var now = _timeProvider.GetUtcNow();

        await _unitOfWork.ExecuteInTransactionAsync(async transaction =>
        {
            // 1. Eliminación física en cascada en PostgreSQL
            await _userAccountRepository.DeleteAsync(user.Id, transaction, cancellationToken);

            // 2. Registro de USER_DELETED aislado con SAVEPOINT (DP-TRANS-001)
            const string savepointName = "sp_user_deleted";
            var savepointCreated = false;

            try
            {
                if (transaction is not null && transaction.SupportsSavepoints)
                {
                    await transaction.SaveAsync(savepointName, cancellationToken);
                    savepointCreated = true;
                }

                var auditEvent = AuditEvent.CreateUserDeleted(now);
                await _auditEventRepository.AddAsync(auditEvent, transaction, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (
                cancellationToken.IsCancellationRequested ||
                ex is OutOfMemoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (savepointCreated && transaction is not null && transaction.SupportsSavepoints)
                {
                    await transaction.RollbackAsync(savepointName, cancellationToken);
                }

                // DP-TRANS-001: La falla recuperable en registrar USER_DELETED no debe revertir la supresión de la cuenta.
                // Observabilidad segura: no se registran contraseñas ni datos identificativos de usuario.
                _logger?.LogWarning(
                    ex,
                    "Fallo recuperable al registrar USER_DELETED durante la supresión de cuenta; se confirma la supresión conforme a DP-TRANS-001.");
            }
        }, cancellationToken);

        // 3. Post-COMMIT: intento de revocación del JTI en Redis
        if (!string.IsNullOrWhiteSpace(command.AccessToken))
        {
            try
            {
                var validation = _jwtTokenService.ValidateAccessToken(command.AccessToken);
                if (validation.IsValid)
                {
                    await _accessTokenDenylist.RevokeAsync(
                        validation.TokenId,
                        validation.ExpiresAt,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Observabilidad segura: no se registran tokens, contraseñas ni datos personales en el log.
                _logger?.LogWarning(
                    ex,
                    "Fallo al revocar el identificador de token en Redis tras confirmarse la supresión de cuenta. La ausencia física del usuario en base de datos previene nuevos accesos.");
            }
        }
    }
}

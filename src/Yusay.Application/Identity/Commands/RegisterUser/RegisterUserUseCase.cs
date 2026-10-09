using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Application.Identity.Commands.RegisterUser;

public sealed class RegisterUserUseCase(
    IUnitOfWork unitOfWork,
    IUserAccountRepository userAccountRepository,
    IUserCredentialRepository userCredentialRepository,
    IEmailVerificationTokenRepository tokenRepository,
    IAuditEventRepository auditEventRepository,
    IPasswordHasher passwordHasher,
    ISecureTokenService tokenService,
    IEmailVerificationSender emailVerificationSender) : IRegisterUserUseCase
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;
    private readonly IUserCredentialRepository _userCredentialRepository = userCredentialRepository;
    private readonly IEmailVerificationTokenRepository _tokenRepository = tokenRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ISecureTokenService _tokenService = tokenService;
    private readonly IEmailVerificationSender _emailVerificationSender = emailVerificationSender;

    public async Task<RegisterUserResult> ExecuteAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        if (!command.AdultConfirmed)
        {
            throw new ValidationException("La confirmación de mayoría de edad (≥ 18 años) es obligatoria para registrar la cuenta.");
        }

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
        {
            throw new ValidationException("La contraseña debe tener al menos 8 caracteres.");
        }

        var email = Email.Create(command.Email);
        var passwordHash = _passwordHasher.HashPassword(command.Password);

        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var now = DateTimeOffset.UtcNow;
        var adultConfirmedAt = command.AdultConfirmedAt ?? now;

        var user = UserAccount.Create(email, adultConfirmedAt, createdAt: now);
        var credential = UserCredential.Create(user.Id, passwordHash, changedAt: now);
        var verificationToken = EmailVerificationToken.Create(user.Id, tokenHash, createdAt: now);

        var userRegisteredAudit = AuditEvent.CreateUserRegistered(user.Id, now);
        var tokenIssuedAudit = AuditEvent.CreateEmailVerificationTokenIssued(user.Id, now);

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var emailExists = await _userAccountRepository.ExistsByEmailAsync(email, tx, cancellationToken);
            if (emailExists)
            {
                throw new ConflictException("Ya existe una cuenta registrada con el correo electrónico proporcionado.");
            }

            await _userAccountRepository.AddAsync(user, tx, cancellationToken);
            await _userCredentialRepository.AddAsync(credential, tx, cancellationToken);

            await _tokenRepository.InvalidateAllForUserAsync(user.Id, tx, cancellationToken);
            await _tokenRepository.AddAsync(verificationToken, tx, cancellationToken);

            await _auditEventRepository.AddAsync(userRegisteredAudit, tx, cancellationToken);
            await _auditEventRepository.AddAsync(tokenIssuedAudit, tx, cancellationToken);
        }, cancellationToken);

        await _emailVerificationSender.SendVerificationTokenAsync(user.Email.Value, rawToken, cancellationToken);
        return new RegisterUserResult(user.Id, user.Email.Value, rawToken);
    }
}

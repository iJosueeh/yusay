using Yusay.Domain.Common;

namespace Yusay.Domain.Identity.Entities;

public sealed class EmailVerificationToken
{
    public static readonly TimeSpan ValidityDuration = TimeSpan.FromHours(24);

    public Guid VerificationTokenId { get; }
    public Guid UserId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    private EmailVerificationToken(
        Guid verificationTokenId,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        VerificationTokenId = verificationTokenId;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static EmailVerificationToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        Guid? tokenId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador de usuario para el token no puede ser un UUID vacío.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("El hash del token de verificación no puede estar vacío.");
        }

        Guid id = tokenId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador del token no puede ser un UUID vacío.");
        }

        DateTimeOffset expiresAt = createdAt.Add(ValidityDuration);

        return new EmailVerificationToken(
            verificationTokenId: id,
            userId: userId,
            tokenHash: tokenHash.Trim(),
            createdAt: createdAt,
            expiresAt: expiresAt);
    }

    public static EmailVerificationToken Rehydrate(
        Guid verificationTokenId,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (verificationTokenId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Los identificadores del token no pueden ser UUIDs vacíos.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("El hash del token de verificación no puede estar vacío.");
        }

        if (createdAt >= expiresAt)
        {
            throw new DomainException("La fecha de emisión debe ser estrictamente anterior a la expiración.");
        }

        return new EmailVerificationToken(verificationTokenId, userId, tokenHash, createdAt, expiresAt);
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsValid(DateTimeOffset now) => now < ExpiresAt && now >= CreatedAt;
}

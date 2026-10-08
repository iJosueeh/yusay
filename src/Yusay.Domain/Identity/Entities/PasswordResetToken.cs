using Yusay.Domain.Common;

namespace Yusay.Domain.Identity.Entities;

public sealed class PasswordResetToken
{
    public static readonly TimeSpan ValidityDuration = TimeSpan.FromMinutes(30);

    public Guid ResetTokenId { get; }
    public Guid UserId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    private PasswordResetToken(
        Guid resetTokenId,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ResetTokenId = resetTokenId;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static PasswordResetToken Create(
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
            throw new DomainException("El hash del token de recuperación no puede estar vacío.");
        }

        Guid id = tokenId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador del token no puede ser un UUID vacío.");
        }

        DateTimeOffset expiresAt = createdAt.Add(ValidityDuration);

        return new PasswordResetToken(
            resetTokenId: id,
            userId: userId,
            tokenHash: tokenHash.Trim(),
            createdAt: createdAt,
            expiresAt: expiresAt);
    }

    public static PasswordResetToken Rehydrate(
        Guid resetTokenId,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (resetTokenId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Los identificadores del token no pueden ser UUIDs vacíos.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("El hash del token de recuperación no puede estar vacío.");
        }

        if (createdAt >= expiresAt)
        {
            throw new DomainException("La fecha de emisión debe ser estrictamente anterior a la expiración.");
        }

        return new PasswordResetToken(resetTokenId, userId, tokenHash, createdAt, expiresAt);
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsValid(DateTimeOffset now) => now < ExpiresAt && now >= CreatedAt;
}

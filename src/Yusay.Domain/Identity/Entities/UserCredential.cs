using Yusay.Domain.Common;

namespace Yusay.Domain.Identity.Entities;

public sealed class UserCredential
{
    public Guid UserId { get; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset PasswordChangedAt { get; private set; }

    private UserCredential(Guid userId, string passwordHash, DateTimeOffset passwordChangedAt)
    {
        UserId = userId;
        PasswordHash = passwordHash;
        PasswordChangedAt = passwordChangedAt;
    }

    public static UserCredential Create(
        Guid userId,
        string passwordHash,
        DateTimeOffset? changedAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador de cuenta para la credencial no puede ser un UUID vacío.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("El hash de la contraseña es obligatorio y no puede estar vacío.");
        }

        DateTimeOffset instant = changedAt ?? DateTimeOffset.UtcNow;

        return new UserCredential(userId, passwordHash.Trim(), instant);
    }

    public static UserCredential Rehydrate(
        Guid userId,
        string passwordHash,
        DateTimeOffset passwordChangedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador de cuenta para la credencial no puede ser un UUID vacío.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("El hash de la contraseña no puede estar vacío.");
        }

        return new UserCredential(userId, passwordHash, passwordChangedAt);
    }

    public void ChangePassword(string newPasswordHash, DateTimeOffset changedAt)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException("El nuevo hash de contraseña es obligatorio y no puede estar vacío.");
        }

        if (changedAt < PasswordChangedAt)
        {
            throw new DomainException("El instante de cambio de contraseña no puede ser anterior al cambio previo.");
        }

        PasswordHash = newPasswordHash.Trim();
        PasswordChangedAt = changedAt;
    }
}

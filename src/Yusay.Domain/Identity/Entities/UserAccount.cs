using Yusay.Domain.Common;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Domain.Identity.Entities;

public sealed class UserAccount
{
    public Guid Id { get; }
    public Email Email { get; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset AdultConfirmedAt { get; }
    public UserAccountStatus Status { get; private set; }
    
    public bool IsEmailVerified => EmailVerifiedAt.HasValue;
    public bool CanAccessPersonalFeatures => Status == UserAccountStatus.Active && IsEmailVerified;

    private UserAccount(
        Guid id,
        Email email,
        DateTimeOffset? emailVerifiedAt,
        DateTimeOffset createdAt,
        DateTimeOffset adultConfirmedAt,
        UserAccountStatus status)
    {
        Id = id;
        Email = email;
        EmailVerifiedAt = emailVerifiedAt;
        CreatedAt = createdAt;
        AdultConfirmedAt = adultConfirmedAt;
        Status = status;
    }

    public static UserAccount Create(
        Email email,
        DateTimeOffset adultConfirmedAt,
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        if (adultConfirmedAt == default || adultConfirmedAt == DateTimeOffset.MinValue)
        {
            throw new DomainException("La confirmación de mayoría de edad (≥ 18 años) es obligatoria para registrar la cuenta.");
        }

        Guid userId = id ?? Guid.NewGuid();
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador de cuenta no puede ser un UUID vacío.");
        }

        DateTimeOffset creationInstant = createdAt ?? DateTimeOffset.UtcNow;

        return new UserAccount(
            id: userId,
            email: email,
            emailVerifiedAt: null,
            createdAt: creationInstant,
            adultConfirmedAt: adultConfirmedAt,
            status: UserAccountStatus.Active);
    }

    public static UserAccount Rehydrate(
        Guid id,
        Email email,
        DateTimeOffset? emailVerifiedAt,
        DateTimeOffset createdAt,
        DateTimeOffset adultConfirmedAt,
        UserAccountStatus status)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador de cuenta no puede ser un UUID vacío.");
        }

        return new UserAccount(id, email, emailVerifiedAt, createdAt, adultConfirmedAt, status);
    }

    public void VerifyEmail(DateTimeOffset verifiedAt)
    {
        if (verifiedAt < CreatedAt)
        {
            throw new DomainException("El instante de verificación de correo no puede ser anterior a la creación de la cuenta.");
        }

        EmailVerifiedAt = verifiedAt;
    }

    public void Block()
    {
        Status = UserAccountStatus.Blocked;
    }

    public void Unblock()
    {
        Status = UserAccountStatus.Active;
    }
}

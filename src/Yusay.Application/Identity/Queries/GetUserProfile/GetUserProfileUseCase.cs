using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Dtos;
using Yusay.Application.Identity.Repositories;

namespace Yusay.Application.Identity.Queries.GetUserProfile;

public sealed class GetUserProfileUseCase(
    ICurrentUser currentUser,
    IUserAccountRepository userAccountRepository) : IGetUserProfileUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;

    public async Task<UserProfileDto> ExecuteAsync(
        GetUserProfileQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para acceder al perfil.");
        }

        var user = await _userAccountRepository.GetByIdAsync(userId.Value, transaction: null, cancellationToken) ?? throw new UnauthorizedException("Usuario no encontrado.");

        return new UserProfileDto(
            user.Id,
            user.Email.Value,
            user.IsEmailVerified,
            user.CreatedAt,
            user.AdultConfirmedAt,
            user.Status.ToString().ToUpperInvariant());
    }
}

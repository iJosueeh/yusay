using Yusay.Application.Identity.Dtos;

namespace Yusay.Application.Identity.Queries.GetUserProfile;

public interface IGetUserProfileUseCase
{
    Task<UserProfileDto> ExecuteAsync(GetUserProfileQuery query, CancellationToken cancellationToken = default);
}

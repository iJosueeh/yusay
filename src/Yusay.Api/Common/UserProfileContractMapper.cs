using Yusay.Api.Contracts.Identity;
using Yusay.Application.Identity.Dtos;

namespace Yusay.Api.Common;

public static class UserProfileContractMapper
{
    public static UserProfileResponse ToResponse(UserProfileDto dto) =>
        new(
            dto.UserId,
            dto.Email,
            dto.EmailVerified,
            dto.CreatedAt,
            dto.AdultConfirmedAt,
            dto.Status);
}

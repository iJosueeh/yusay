namespace Yusay.Application.Identity.Queries.ValidateAccessToken;

public interface IValidateAccessTokenUseCase
{
    Task<ValidateAccessTokenResult> ExecuteAsync(ValidateAccessTokenQuery query, CancellationToken cancellationToken = default);
}

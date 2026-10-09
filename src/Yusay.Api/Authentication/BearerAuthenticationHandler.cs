using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Queries.ValidateAccessToken;

namespace Yusay.Api.Authentication;

public sealed class BearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Bearer";

    private const string BearerPrefix = "Bearer ";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var endpoint = Context.GetEndpoint();
        if (endpoint is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var metadata = endpoint.Metadata;
        if (metadata.GetMetadata<AllowAnonymousAttribute>() is not null
            || metadata.GetMetadata<IAuthorizeData>() is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            || authorization[BearerPrefix.Length..].Trim().Length == 0)
        {
            throw new UnauthorizedException(MissingBearerMessage);
        }

        return ValidateAndAuthenticateAsync(authorization[BearerPrefix.Length..].Trim());
    }

    private async Task<AuthenticateResult> ValidateAndAuthenticateAsync(string accessToken)
    {

        var validateAccessToken = Context.RequestServices
            .GetRequiredService<IValidateAccessTokenUseCase>();

        var result = await validateAccessToken.ExecuteAsync(
            new ValidateAccessTokenQuery(accessToken),
            Context.RequestAborted);

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                new Claim(ClaimTypes.Name, result.Email)
            ],
            authenticationType: Scheme.Name);

        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties { ExpiresUtc = result.ExpiresAt };

        return AuthenticateResult.Success(new AuthenticationTicket(principal, properties, Scheme.Name));
    }
}

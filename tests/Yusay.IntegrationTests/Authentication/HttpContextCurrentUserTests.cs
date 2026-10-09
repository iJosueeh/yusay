using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Yusay.Api.Common;
using Yusay.Application.Common.Interfaces;

namespace Yusay.IntegrationTests.Authentication;

/// <summary>
/// Pruebas unitarias del adaptador de identidad corriente (F1 de N1, OQ-ARCH-017): lectura
/// exclusiva de <c>ClaimTypes.NameIdentifier</c> del principal autenticado, <c>null</c> ante
/// ausencia de identidad o claim inválido (nunca <c>Guid.Empty</c> como identidad válida) y
/// dependencias limitadas a <c>IHttpContextAccessor</c> — sin segunda validación JWT ni
/// consulta adicional a Redis.
/// </summary>
public sealed class HttpContextCurrentUserTests
{
    [Fact]
    public void WithoutHttpContext_ShouldReturnNull()
    {
        var currentUser = CreateAdapter(httpContext: null);

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void WithoutIdentity_ShouldReturnNull()
    {
        // Principal vacío: Identity es null
        var currentUser = CreateAdapter(new DefaultHttpContext
        {
            User = new ClaimsPrincipal()
        });

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void UnauthenticatedPrincipal_ShouldReturnNull()
    {
        // Identidad sin tipo de autenticación: IsAuthenticated es false aunque traiga claims
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")));
        var currentUser = CreateAdapter(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        });

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void AuthenticatedWithoutNameIdentifierClaim_ShouldReturnNull()
    {
        var identity = new ClaimsIdentity(authenticationType: "Bearer");
        identity.AddClaim(new Claim(ClaimTypes.Name, "user@yusay.local"));
        var currentUser = CreateAdapter(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        });

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void AuthenticatedWithNonGuidNameIdentifier_ShouldReturnNull()
    {
        var identity = new ClaimsIdentity(authenticationType: "Bearer");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "no-es-un-guid"));
        var currentUser = CreateAdapter(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        });

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void AuthenticatedWithEmptyGuidNameIdentifier_ShouldReturnNull()
    {
        var identity = new ClaimsIdentity(authenticationType: "Bearer");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString("D")));
        var currentUser = CreateAdapter(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        });

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void AuthenticatedWithValidNameIdentifier_ShouldReturnTheClaimUserId()
    {
        var expectedUserId = Guid.NewGuid();
        var identity = new ClaimsIdentity(authenticationType: "Bearer");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, expectedUserId.ToString("D")));
        var currentUser = CreateAdapter(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        });

        Assert.Equal(expectedUserId, currentUser.UserId);
    }

    [Fact]
    public void AdapterConstructor_ShouldOnlyRequireHttpContextAccessor_NoSecondJwtValidationNorRedisQuery()
    {
        // El adaptador no puede validar el JWT ni consultar la denylist de Redis: su única
        // dependencia es el accessor de contexto. La validación única por petición sigue
        // siendo responsabilidad de BearerAuthenticationHandler → IValidateAccessTokenUseCase.
        var parameters = typeof(HttpContextCurrentUser)
            .GetConstructors()
            .Single()
            .GetParameters();

        Assert.Single(parameters);
        Assert.Equal(typeof(IHttpContextAccessor), parameters[0].ParameterType);
    }

    private static HttpContextCurrentUser CreateAdapter(HttpContext? httpContext) =>
        new(new StubHttpContextAccessor { HttpContext = httpContext });

    private sealed class StubHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}

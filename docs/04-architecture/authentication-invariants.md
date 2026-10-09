# Invariantes de autenticación HTTP (Bearer)

**Status: ACTIVE — cierre de revisión de `BearerAuthenticationHandler` con el pipeline de autenticación y autorización de ASP.NET Core**

Este documento registra los invariantes que sostienen la autenticación Bearer reutilizable de Yusay (`src/Yusay.Api/Authentication/BearerAuthenticationHandler.cs`). Incumplir cualquiera de ellos rompe garantías ya verificadas por pruebas; cualquier cambio que los afecte requiere revisión explícita antes de implementarse.

## Invariantes

### 1. `UseExceptionHandler` debe envolver `UseAuthentication` y `UseAuthorization`

El esquema Bearer distingue los fallos **lanzando excepciones** —`UnauthorizedException` → 401 y `ServiceUnavailableException` → 503 (fail-closed ante Redis no disponible)— y `ApiExceptionHandler`, registrado el primero en el pipeline (`Program.cs`), las convierte en ProblemDetails con `traceId`. Si la excepción de autenticación quedara fuera de su alcance (p. ej. moviendo `UseAuthentication` antes de `UseExceptionHandler`), el 401/503 dejaría de emitirse en el formato ProblemDetails del resto de la API.

### 2. `SignOut` no debe llevar `[Authorize]`

Su caso de uso (`SignOutUseCase`) realiza la validación del token y conserva la **idempotencia**: token expirado o previamente revocado → 204 sin revocación nueva ni re-auditoría, con una única auditoría `SIGN_OUT` por cierre efectivo. Si se protegiera con `[Authorize]`:

- la validación se duplicaría (pipeline de autenticación + caso de uso), y
- el 204 idempotente de los tokens expirados/ya revocados se convertiría en un 401 de pipeline, alterando el contrato aprobado.

El esquema Bearer, de hecho, **no valida** endpoints sin `IAuthorizeData`, por lo que SignOut recibe exactamente una validación, la suya.

### 3. El esquema autentica únicamente endpoints con `IAuthorizeData` y respeta `[AllowAnonymous]`

- `AllowAnonymous` tiene **precedencia** en el *gate* (se comprueba antes que `IAuthorizeData`) y también la tiene `AuthorizationMiddleware` frente a cualquier política: un endpoint con `[AllowAnonymous]` responde sin token incluso dentro de un controlador con `[Authorize]`, y **sin intentar validar** (cero consultas a JWT/Redis en endpoints públicos).
- Los endpoints públicos actuales (register, verify-email, sign-in, password-reset, health y sign-out) no reciben validación en el pipeline: una cabecera `Authorization` inválida o la indisponibilidad de Redis no alteran sus respuestas existentes.

### 4. Una futura `FallbackPolicy` requiere revisar este comportamiento antes de activarse

Con el *gate* actual, si se registrara una `FallbackPolicy` (p. ej. `RequireAuthenticatedUser`), los endpoints públicos pasarían a evaluarse contra ella: **no hay bypass** (la política denegaría toda request sin autenticar —dirección fail-closed—), pero un **token válido** en un endpoint público devolvería `NoResult` y la política lo denegaría con 401, y ese 401 saldría del challenge por defecto **sin ProblemDetails**. Antes de activar una `FallbackPolicy` debe decidirse el formato del challenge y si el *gate* pasa a autenticar cuando exista cabecera Bearer (con *opt-out* explícito para SignOut), o si la política se restringe a endpoints con `[Authorize]`.

### 5. Los futuros `HandleChallengeAsync` y `HandleForbiddenAsync` deberán emitir ProblemDetails con `traceId`

Hoy son inalcanzables: el esquema nunca devuelve `Fail` (los fallos se lanzan) y no existen reglas de autorización ni escenarios de denegación (403). Cuando se incorporen roles, políticas o *fallback*, sus implementaciones deberán conservar el contrato de error de la API (ProblemDetails + `traceId`, `application/problem+json`) en lugar del 401/403 vacío por defecto.

## Verificación

- `tests/Yusay.IntegrationTests/Http/BearerAuthenticationHttpTests.cs` — 401/200/503 del endpoint protegido, `[Authorize]` a nivel de acción y de controlador, precedencia de `[AllowAnonymous]`, accesibilidad de los públicos con cabecera inválida, e idempotencia de SignOut (204/204 con una sola auditoría `SIGN_OUT`).
- `tests/Yusay.IntegrationTests/Http/SignOutHttpEndpointsTests.cs` — 204 con auditoría y fail-closed 503.
- El documento OpenAPI se verifica exacto (7 rutas): las sondas `[Authorize]` viven solo en el ensamblado de pruebas.

## Restricciones de evolución

Sin autorización por roles ni `FallbackPolicy` mientras sus reglas no estén aprobadas (OQ-ARCH-017); sin refresh tokens ni cookies; la validación JWT permanece centralizada en `IValidateAccessTokenUseCase` sin mecanismos paralelos.

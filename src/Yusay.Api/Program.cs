using Dapper;
using Microsoft.AspNetCore.Authentication;
using Yusay.Api.Authentication;
using Yusay.Api.Common;
using Yusay.Application;
using Yusay.Application.Common.Interfaces;
using Yusay.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddAuthentication(BearerAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerAuthenticationHandler>(
        BearerAuthenticationHandler.SchemeName,
        options => { });

// Identidad corriente (F1 de N1, OQ-ARCH-017): el adaptador lee el principal ya validado por
// el handler —sin segunda validación JWT ni consulta a Redis— y se registra con ciclo de vida
// scoped, igual que la petición que lo resuelve.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (IDbConnectionFactory connectionFactory) =>
{
    try
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync();
        var result = await connection.QuerySingleAsync<HealthStatus>(
            """
            SELECT 
                version() AS PostgresVersion,
                current_schema() AS CurrentSchema,
                count(*)::int AS TableCount
            FROM information_schema.tables 
            WHERE table_schema = 'yusay';
            """
        );

        return Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            database = new
            {
                connected = true,
                schema = result.CurrentSchema,
                normativeTables = result.TableCount,
                version = result.PostgresVersion
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database connection failed"
        );
    }
})
.WithName("HealthCheck");

app.MapControllers();
app.Run();

internal sealed record HealthStatus(string PostgresVersion, string CurrentSchema, int TableCount);
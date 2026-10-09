using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Npgsql;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Application.Identity.Tokens;
using Yusay.Application.Tracking.Repositories;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;

namespace Yusay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var connectionString = ResolvePostgresConnectionString(configuration);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditEventRepository, Audit.Repositories.AuditEventRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<ICheckInRepository, CheckInRepository>();
        services.AddScoped<IDimensionVersionRepository, DimensionVersionRepository>();

        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();
        // PROVISIONALES — sin proveedor real de correo: descartan los tokens sin enviar nada.
        // NO APTOS PARA PRODUCCIÓN hasta incorporar un proveedor externo con outbox/reintentos.
        services.AddSingleton<IEmailVerificationSender, Emailing.NullEmailVerificationSender>();
        services.AddSingleton<IPasswordResetEmailSender, Emailing.NullPasswordResetEmailSender>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(CreateJwtOptions(configuration));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        var redisConfiguration = ResolveRedisConfiguration(configuration);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConfiguration);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IAccessTokenDenylist, RedisAccessTokenDenylist>();

        return services;
    }

    private static string ResolveRedisConfiguration(IConfiguration configuration)
    {
        var url = ResolveEnvironmentValue(configuration, "REDIS_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var host = ResolveEnvironmentValue(configuration, "REDIS_HOST") ?? "localhost";
        var port = ResolveEnvironmentValue(configuration, "REDIS_PORT") ?? "6379";
        var password = ResolveEnvironmentValue(configuration, "REDIS_PASSWORD");

        var endpoint = $"{host}:{port}";

        return string.IsNullOrWhiteSpace(password) ? endpoint : $"{endpoint},password={password}";
    }

    private static JwtOptions CreateJwtOptions(IConfiguration configuration)
    {
        return new JwtOptions
        {
            Secret = ResolveEnvironmentValue(configuration, "JWT_SECRET") ?? string.Empty,
            Issuer = ResolveEnvironmentValue(configuration, "JWT_ISSUER") ?? JwtOptions.DefaultIssuer,
            Audience = ResolveEnvironmentValue(configuration, "JWT_AUDIENCE") ?? JwtOptions.DefaultAudience,
            AccessTokenLifetimeSeconds =
                int.TryParse(ResolveEnvironmentValue(configuration, "JWT_ACCESS_TOKEN_TTL_SECONDS"), out var lifetimeSeconds)
                    ? lifetimeSeconds
                    : JwtOptions.DefaultAccessTokenLifetimeSeconds
        };
    }

    private static string? ResolveEnvironmentValue(IConfiguration configuration, string name)
    {
        var value = configuration[name] ?? Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ResolvePostgresConnectionString(IConfiguration configuration)
    {
        var databaseUrl = configuration["DATABASE_URL"] 
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            var host = configuration["POSTGRES_HOST"] ?? "localhost";
            var port = configuration["POSTGRES_PORT"] ?? "5432";
            var db = configuration["POSTGRES_DB"] ?? "yusay";
            var user = configuration["DB_USER"] ?? "postgres";
            var pass = configuration["DB_PASSWORD"] ?? "postgres_dev_password";

            return $"Host={host};Port={port};Database={db};Username={user};Password={pass};Search Path=yusay,pg_temp;";
        }

        if (databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertUriToNpgsqlConnectionString(databaseUrl);
        }

        if (!databaseUrl.Contains("Search Path", StringComparison.OrdinalIgnoreCase))
        {
            databaseUrl = databaseUrl.TrimEnd(';') + ";Search Path=yusay,pg_temp;";
        }

        return databaseUrl;
    }

    private static string ConvertUriToNpgsqlConnectionString(string uriString)
    {
        var uri = new Uri(uriString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};Search Path=yusay,pg_temp;";
    }
}

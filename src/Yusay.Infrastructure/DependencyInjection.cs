using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;

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

        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();

        return services;
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

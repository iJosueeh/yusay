using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using Yusay.Application.Common.Interfaces;
using Yusay.Infrastructure.Persistence;

namespace Yusay.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    /// <summary>
    /// Credencial única de los roles técnicos reales de la colección (espejo de
    /// <c>database/init/01-init-roles.sh</c>), exclusiva de la base de pruebas del
    /// contenedor. Permite verificar permisos efectivos conectándose como
    /// <c>yusay_app</c>, <c>yusay_worker</c> y <c>yusay_admin_ops</c> (B8b-1).
    /// </summary>
    public const string TechnicalRolePassword = "yusay_roles_test_password_2026";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("yusay_test")
        .WithUsername("postgres")
        .WithPassword("test_password")
        .Build();

    public IDbConnectionFactory ConnectionFactory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString() + ";Search Path=yusay,pg_temp;";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
        var dataSource = dataSourceBuilder.Build();
        ConnectionFactory = new NpgsqlConnectionFactory(dataSource);

        // yusay_admin_ops debe existir ANTES de las migraciones: V019 solo concede
        // privilegios si el rol ya está creado (Flyway no crea roles).
        await CreateOperationalAdminRoleAsync();
        await ApplyMigrationsAsync();
        // V001 crea yusay_app/yusay_worker sin LOGIN; la colección les habilita la
        // conexión con credencial de prueba para verificar permisos efectivos reales.
        await EnableTechnicalRoleConnectionsAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Cadena de conexión del contenedor autenticándose como un rol técnico con la
    /// credencial de pruebas (<see cref="TechnicalRolePassword"/>).
    /// </summary>
    public string BuildRoleConnectionString(string username) =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = username,
            Password = TechnicalRolePassword
        }.ConnectionString;

    private async Task CreateOperationalAdminRoleAsync()
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync();
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pg_catalog.pg_roles WHERE rolname = 'yusay_admin_ops'");

        if (exists == 0)
        {
            await connection.ExecuteAsync(
                "CREATE ROLE yusay_admin_ops WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOREPLICATION PASSWORD '" +
                TechnicalRolePassword + "'");
        }
    }

    private async Task EnableTechnicalRoleConnectionsAsync()
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync();

        foreach (var role in new[] { "yusay_app", "yusay_worker" })
        {
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM pg_catalog.pg_roles WHERE rolname = @Role",
                new { Role = role });

            if (exists > 0)
            {
                await connection.ExecuteAsync(
                    "ALTER ROLE " + role + " WITH LOGIN PASSWORD '" + TechnicalRolePassword + "'");
            }
        }
    }

    private async Task ApplyMigrationsAsync()
    {
        var migrationsDir = FindMigrationsPath();
        var migrationFiles = Directory.GetFiles(migrationsDir, "V*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync();

        foreach (var file in migrationFiles)
        {
            var sql = await File.ReadAllTextAsync(file);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string FindMigrationsPath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null && !File.Exists(Path.Combine(current.FullName, "Yusay.slnx")))
        {
            current = current.Parent;
        }

        if (current == null)
        {
            throw new InvalidOperationException("No se encontró la raíz del proyecto para localizar las migraciones.");
        }

        return Path.Combine(current.FullName, "database", "migrations");
    }
}

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>
{
}
